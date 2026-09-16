using System.Security.Cryptography;
using System.Text;
using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Routes;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Routes;

public interface IRouteService
{
    Task<IReadOnlyList<RouteResponse>> ListAsync(CancellationToken ct = default);
    Task<RouteResponse> GetAsync(string id, CancellationToken ct = default);
    Task<RouteResponse> CreateAsync(CreateRouteRequest request, CancellationToken ct = default);
    Task<RouteResponse> UpdateAsync(string id, UpdateRouteRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// the only writer of Enabled, a normal edit must never flip it
    Task<RouteResponse> SetEnabledAsync(string id, bool enabled, CancellationToken ct = default);

    /// no side effects
    Task<string> RenderConfigAsync(CancellationToken ct = default);

    /// deploys or rolls the nginx edge with the current routes
    Task<WorkloadRuntimeStatus> ApplyAsync(CancellationToken ct = default);

    Task<WorkloadRuntimeStatus> GetProxyStatusAsync(CancellationToken ct = default);

    /// status plus whether saved routes and certs have reached the running edge
    Task<ProxyStateResponse> GetProxyStateAsync(CancellationToken ct = default);
}

public sealed class RouteService(
    IRouteRepository routes,
    IWorkloadRepository workloads,
    IReverseProxy reverseProxy,
    ICertificateService certificates,
    IWorkloadBackendResolver backends,
    IBasicAuthHasher basicAuthHasher,
    IPlatformSettingsRepository settings,
    IOptions<ClusterOptions> clusterOptions,
    IOptions<ReverseProxyOptions> proxyOptions,
    TimeProvider clock) : IRouteService
{
    private readonly string _network = clusterOptions.Value.OverlayNetwork;
    private readonly ReverseProxyOptions _proxy = proxyOptions.Value;

    public async Task<IReadOnlyList<RouteResponse>> ListAsync(CancellationToken ct = default)
    {
        var all = await routes.ListAsync(ct);
        var names = await WorkloadNamesAsync(ct);
        return all.Select(r => Map(r, names)).ToList();
    }

    public async Task<RouteResponse> GetAsync(string id, CancellationToken ct = default)
    {
        var route = await GetExistingAsync(id, ct);
        return Map(route, await WorkloadNamesAsync(ct));
    }

    public async Task<RouteResponse> CreateAsync(CreateRouteRequest request, CancellationToken ct = default)
    {
        var hostname = ValidateHostname(request.Hostname);
        var path = ValidatePath(request.Path);
        ValidatePort(request.TargetPort);
        var (workloadId, upstreamHost) = await ValidateTargetAsync(request.Target, request.WorkloadId, request.UpstreamHost, ct);

        if (await routes.ExistsAsync(hostname, path, excludingId: null, ct))
        {
            throw new ConflictException($"A route for '{hostname}{path}' already exists.");
        }

        var now = clock.GetUtcNow();
        var route = new Route
        {
            Hostname = hostname,
            Path = path,
            Target = request.Target,
            WorkloadId = workloadId,
            UpstreamHost = upstreamHost,
            UpstreamScheme = request.UpstreamScheme,
            TargetPort = request.TargetPort,
            CreatedAt = now,
            UpdatedAt = now,
        };
        ApplyHttpOptions(route, request.WebSockets, request.MaxBodySizeMb, request.BasicAuthEnabled, request.BasicAuthUsername, request.BasicAuthPassword);

        await routes.AddAsync(route, ct);
        return Map(route, await WorkloadNamesAsync(ct));
    }

    public async Task<RouteResponse> UpdateAsync(string id, UpdateRouteRequest request, CancellationToken ct = default)
    {
        var route = await GetExistingAsync(id, ct);
        var hostname = ValidateHostname(request.Hostname);
        var path = ValidatePath(request.Path);
        ValidatePort(request.TargetPort);
        var (workloadId, upstreamHost) = await ValidateTargetAsync(request.Target, request.WorkloadId, request.UpstreamHost, ct);

        if (await routes.ExistsAsync(hostname, path, excludingId: id, ct))
        {
            throw new ConflictException($"A route for '{hostname}{path}' already exists.");
        }

        route.Hostname = hostname;
        route.Path = path;
        route.Target = request.Target;
        route.WorkloadId = workloadId;
        route.UpstreamHost = upstreamHost;
        route.UpstreamScheme = request.UpstreamScheme;
        route.TargetPort = request.TargetPort;
        route.UpdatedAt = clock.GetUtcNow();
        ApplyHttpOptions(route, request.WebSockets, request.MaxBodySizeMb, request.BasicAuthEnabled, request.BasicAuthUsername, request.BasicAuthPassword);

        await routes.UpdateAsync(route, ct);
        return Map(route, await WorkloadNamesAsync(ct));
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
        => await routes.RemoveAsync(await GetExistingAsync(id, ct), ct);

    public async Task<RouteResponse> SetEnabledAsync(string id, bool enabled, CancellationToken ct = default)
    {
        var route = await GetExistingAsync(id, ct);
        route.Enabled = enabled;
        route.UpdatedAt = clock.GetUtcNow();
        await routes.UpdateAsync(route, ct);
        return Map(route, await WorkloadNamesAsync(ct));
    }

    public async Task<string> RenderConfigAsync(CancellationToken ct = default)
        => (await BuildDesiredAsync(ct)).Config;

    /// deploy and fingerprint both read this so they can't disagree on what's current
    private async Task<DesiredProxy> BuildDesiredAsync(CancellationToken ct)
    {
        var (routable, names) = await LoadRoutableAsync(ct);

        // one list drives both ssl_certificate lines and mounts
        // a missing cert file stops nginx and every other site with it
        var installable = await certificates.InstallableAsync(ct);
        var config = reverseProxy.Render(ToConfigs(routable, names), [.. installable.Select(ToHostCertificate)]);

        var mounts = new List<ConfigMount> { new() { Name = "nginx", Content = config, Path = _proxy.ConfigPath } };
        foreach (var r in routable.Where(HasAuth))
        {
            mounts.Add(new ConfigMount { Name = $"auth-{r.Id}", Content = $"{r.BasicAuthUsername}:{r.BasicAuthPasswordHash}\n", Path = AuthPath(r) });
        }

        var secrets = new List<SecretMount>();
        foreach (var certificate in installable)
        {
            secrets.Add(new SecretMount
            {
                Name = $"cert-{certificate.Hostname}",
                Content = certificate.PemChain,
                FileName = CertificateFileName(certificate.Hostname),
            });
            secrets.Add(new SecretMount
            {
                Name = $"key-{certificate.Hostname}",
                Content = certificate.PrivateKeyPem,
                FileName = PrivateKeyFileName(certificate.Hostname),
            });
        }

        return new DesiredProxy(config, mounts, secrets, routable.Count);
    }

    private sealed record DesiredProxy(string Config, List<ConfigMount> Mounts, List<SecretMount> Secrets, int RouteCount)
    {
        /// includes file contents, the config alone misses a renewed cert or changed password
        public string Fingerprint()
        {
            var payload = new StringBuilder();
            foreach (var m in Mounts.OrderBy(m => m.Name, StringComparer.Ordinal))
            {
                payload.Append(m.Name).Append('\u0000').Append(m.Content).Append('\u0001');
            }

            foreach (var s in Secrets.OrderBy(s => s.Name, StringComparer.Ordinal))
            {
                payload.Append(s.Name).Append('\u0000').Append(s.Content).Append('\u0001');
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString())));
        }
    }

    public async Task<WorkloadRuntimeStatus> ApplyAsync(CancellationToken ct = default)
    {
        var desired = await BuildDesiredAsync(ct);
        var mounts = desired.Mounts;
        var secrets = desired.Secrets;

        var deployment = new WorkloadDeployment
        {
            Name = _proxy.ServiceName,
            Kind = WorkloadKind.Container,
            Image = _proxy.Image,
            Mode = _proxy.OnEveryNode ? WorkloadMode.Global : WorkloadMode.Replicated,
            Replicas = 1,
            // nginx is 80/443 inside whatever it's published as
            Ports =
            [
                new PortMapping(_proxy.HttpPort, ReverseProxyPorts.StandardHttp, PortProtocol.Tcp, PortPublishMode.Host),
                new PortMapping(_proxy.HttpsPort, ReverseProxyPorts.StandardHttps, PortProtocol.Tcp, PortPublishMode.Host),
            ],
            Network = _network,
            // global places itself, pinning too would leave it on one node
            PinToControlNode = !_proxy.OnEveryNode,
            Configs = mounts,
            Secrets = secrets,
            Labels = PlatformLabels.Marker,
        };

        var backend = backends.Resolve(WorkloadKind.Container);
        await backend.DeployAsync(deployment, ct);

        // recorded only after the deploy lands so a failed apply still shows pending
        var row = await settings.GetAsync(ct) ?? new PlatformSettings();
        row.AppliedProxyHash = desired.Fingerprint();
        row.ProxyAppliedAt = clock.GetUtcNow();
        await settings.SaveAsync(row, ct);

        return await backend.GetStatusAsync(deployment, ct);
    }

    // swarm mounts secrets by name under /run/secrets, stable names keep the nginx config the same across renewals
    private static string CertificateFileName(string hostname) => $"cert-{hostname}.pem";

    private static string PrivateKeyFileName(string hostname) => $"key-{hostname}.pem";

    private static HostCertificate ToHostCertificate(CertificateMaterial material) => new()
    {
        Hostname = material.Hostname,
        CertificatePath = $"{SecretsDirectory}/{CertificateFileName(material.Hostname)}",
        PrivateKeyPath = $"{SecretsDirectory}/{PrivateKeyFileName(material.Hostname)}",
    };

    private const string SecretsDirectory = "/run/secrets";

    private async Task<(List<Route> Routable, Dictionary<string, string> Names)> LoadRoutableAsync(CancellationToken ct)
    {
        var all = await routes.ListAsync(ct);
        var names = await WorkloadNamesAsync(ct);
        // external routes need no workload, workload routes need theirs to still be a container
        return (all.Where(r => r.Enabled
            && (r.Target == RouteTarget.External || (r.WorkloadId is not null && names.ContainsKey(r.WorkloadId))))
            .ToList(), names);
    }

    private static List<RouteConfig> ToConfigs(IEnumerable<Route> routable, IReadOnlyDictionary<string, string> names) =>
        routable.Select(r => new RouteConfig
        {
            Hostname = r.Hostname,
            Path = r.Path,
            // the namespaced swarm service, not the workload name
            UpstreamService = r.Target == RouteTarget.External
                ? r.UpstreamHost!
                : SwarmNaming.ServiceName(names[r.WorkloadId!]),
            UpstreamPort = r.TargetPort,
            Scheme = r.UpstreamScheme,
            WebSockets = r.WebSockets,
            MaxBodySizeMb = r.MaxBodySizeMb,
            AuthFilePath = HasAuth(r) ? AuthPath(r) : null,
        }).ToList();

    private static bool HasAuth(Route r) =>
        r.BasicAuthEnabled && !string.IsNullOrEmpty(r.BasicAuthUsername) && !string.IsNullOrEmpty(r.BasicAuthPasswordHash);

    private static string AuthPath(Route r) => $"/etc/nginx/auth/{r.Id}.htpasswd";

    private void ApplyHttpOptions(Route route, bool webSockets, int? maxBodySizeMb, bool basicAuthEnabled, string? username, string? password)
    {
        if (maxBodySizeMb < 0)
        {
            throw new ValidationException(nameof(CreateRouteRequest.MaxBodySizeMb), "Max body size can't be negative. Use 0 for unlimited.");
        }

        route.WebSockets = webSockets;
        route.MaxBodySizeMb = maxBodySizeMb;
        route.BasicAuthEnabled = basicAuthEnabled;

        if (!basicAuthEnabled)
        {
            route.BasicAuthUsername = null;
            route.BasicAuthPasswordHash = null;
            return;
        }

        var user = (username ?? string.Empty).Trim();
        if (user.Length == 0)
        {
            throw new ValidationException(nameof(CreateRouteRequest.BasicAuthUsername), "A username is required when basic auth is enabled.");
        }

        route.BasicAuthUsername = user;

        if (!string.IsNullOrEmpty(password))
        {
            route.BasicAuthPasswordHash = basicAuthHasher.Hash(password);
        }
        else if (string.IsNullOrEmpty(route.BasicAuthPasswordHash))
        {
            throw new ValidationException(nameof(CreateRouteRequest.BasicAuthPassword), "A password is required to enable basic auth.");
        }
    }

    public async Task<ProxyStateResponse> GetProxyStateAsync(CancellationToken ct = default)
    {
        var status = await GetProxyStatusAsync(ct);
        var row = await settings.GetAsync(ct);
        var desired = await BuildDesiredAsync(ct);

        return new ProxyStateResponse
        {
            Status = status,
            // no apply on record only counts as drift if there's something to serve
            PendingChanges = row?.AppliedProxyHash is { Length: > 0 } applied
                ? !string.Equals(applied, desired.Fingerprint(), StringComparison.Ordinal)
                : desired.RouteCount > 0,
            LastAppliedAt = row?.ProxyAppliedAt,
        };
    }

    public Task<WorkloadRuntimeStatus> GetProxyStatusAsync(CancellationToken ct = default)
        => backends.Resolve(WorkloadKind.Container)
            .GetStatusAsync(new WorkloadDeployment { Name = _proxy.ServiceName, Kind = WorkloadKind.Container }, ct);

    private async Task<Route> GetExistingAsync(string id, CancellationToken ct)
        => await routes.FindByIdAsync(id, ct) ?? throw new NotFoundException($"Route '{id}' not found.");

    /// containers only, nginx won't start on one unresolvable upstream and takes every site down
    private async Task<Dictionary<string, string>> WorkloadNamesAsync(CancellationToken ct)
        => (await workloads.ListAsync(ct))
            .Where(w => w.Kind == WorkloadKind.Container)
            .ToDictionary(w => w.Id, w => w.Name);

    /// exactly one of the pair is set, per target
    private async Task<(string? WorkloadId, string? UpstreamHost)> ValidateTargetAsync(
        RouteTarget target, string? workloadId, string? upstreamHost, CancellationToken ct)
    {
        if (target == RouteTarget.External)
        {
            return (null, ValidateUpstreamHost(upstreamHost));
        }

        if (string.IsNullOrWhiteSpace(workloadId))
        {
            throw new ValidationException(nameof(CreateRouteRequest.WorkloadId), "Select a workload.");
        }

        await ValidateWorkloadAsync(workloadId, ct);
        return (workloadId, null);
    }

    /// goes straight into proxy_pass, so a scheme, path, port or extra directive is rejected, not escaped
    private static string ValidateUpstreamHost(string? host)
    {
        var value = (host ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            throw new ValidationException(nameof(CreateRouteRequest.UpstreamHost), "A host or IP address is required.");
        }

        if (value.Contains("://", StringComparison.Ordinal) || value.Any(c => c is '/' or ':' or ';' or ' ' or '\t'))
        {
            throw new ValidationException(nameof(CreateRouteRequest.UpstreamHost),
                "Use a bare host or IP — no scheme, port or path. The port is set separately.");
        }

        return value;
    }

    private async Task ValidateWorkloadAsync(string workloadId, CancellationToken ct)
    {
        var workload = await workloads.FindByIdAsync(workloadId, ct)
            ?? throw new ValidationException(nameof(CreateRouteRequest.WorkloadId), "Workload not found.");

        if (workload.Kind != WorkloadKind.Container)
        {
            throw new ValidationException(nameof(CreateRouteRequest.WorkloadId),
                "Only container workloads can be exposed through the reverse proxy.");
        }
    }

    private static string ValidateHostname(string hostname)
    {
        var value = (hostname ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0 || value.Contains(' ') || value.Contains('/'))
        {
            throw new ValidationException(nameof(CreateRouteRequest.Hostname), "A valid hostname is required.");
        }

        return value;
    }

    private static string ValidatePath(string path)
    {
        var value = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (!value.StartsWith('/'))
        {
            throw new ValidationException(nameof(CreateRouteRequest.Path), "Path must start with '/'.");
        }

        return value;
    }

    private static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new ValidationException(nameof(CreateRouteRequest.TargetPort), "Port must be between 1 and 65535.");
        }
    }

    private static RouteResponse Map(Route r, IReadOnlyDictionary<string, string> workloadNames) => new()
    {
        Id = r.Id,
        Hostname = r.Hostname,
        Path = r.Path,
        WorkloadId = r.WorkloadId,
        WorkloadName = r.WorkloadId is null ? null : workloadNames.GetValueOrDefault(r.WorkloadId),
        TargetPort = r.TargetPort,
        WebSockets = r.WebSockets,
        MaxBodySizeMb = r.MaxBodySizeMb,
        Target = r.Target,
        UpstreamHost = r.UpstreamHost,
        UpstreamScheme = r.UpstreamScheme,
        Enabled = r.Enabled,
        BasicAuthEnabled = r.BasicAuthEnabled,
        BasicAuthUsername = r.BasicAuthUsername,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };
}
