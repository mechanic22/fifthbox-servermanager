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

    /// Take a route in or out of the generated config without changing its definition. The only thing
    /// that writes Enabled — an ordinary edit must never flip it as a side effect.
    Task<RouteResponse> SetEnabledAsync(string id, bool enabled, CancellationToken ct = default);

    /// The nginx config for all current routes (no side effects).
    Task<string> RenderConfigAsync(CancellationToken ct = default);

    /// Deploy (or roll) the nginx edge service with the current routes' config. Returns its status.
    Task<WorkloadRuntimeStatus> ApplyAsync(CancellationToken ct = default);

    /// Observed status of the nginx edge service.
    Task<WorkloadRuntimeStatus> GetProxyStatusAsync(CancellationToken ct = default);

    /// Status plus whether saved routes and certificates have reached the running edge.
    Task<ProxyStateResponse> GetProxyStateAsync(CancellationToken ct = default);
}

/// Manages routes (host/path → workload), renders the reverse-proxy config, and deploys the nginx edge
/// service with it. Validates, keeps (host, path) unique, resolves workload ids to swarm service names.
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

    /// Everything the edge should be running: the config, one htpasswd per basic-auth route, and the
    /// certificate material. Deploying and fingerprinting both read this, so they can't disagree about
    /// what "current" means.
    private async Task<DesiredProxy> BuildDesiredAsync(CancellationToken ct)
    {
        var (routable, names) = await LoadRoutableAsync(ct);

        // Only certificates the generator is given get an ssl_certificate line, and only those get their
        // files mounted — one list drives both, so nginx can never be pointed at a file that isn't there.
        // A missing ssl_certificate stops nginx starting at all, taking every other site down with it.
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
        /// Names and contents in order. The config alone would miss a renewed certificate or a changed
        /// basic-auth password — the config only references those by path.
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
            // nginx listens on 80/443 inside the container whatever it's published as.
            Ports =
            [
                new PortMapping(_proxy.HttpPort, ReverseProxyPorts.StandardHttp, PortProtocol.Tcp, PortPublishMode.Host),
                new PortMapping(_proxy.HttpsPort, ReverseProxyPorts.StandardHttps, PortProtocol.Tcp, PortPublishMode.Host),
            ],
            Network = _network,
            // A global edge places itself; pinning it as well would leave one node running it.
            PinToControlNode = !_proxy.OnEveryNode,
            Configs = mounts,
            Secrets = secrets,
            Labels = PlatformLabels.Marker,
        };

        var backend = backends.Resolve(WorkloadKind.Container);
        await backend.DeployAsync(deployment, ct);

        // Recorded only after the deploy lands, so a failed apply keeps reporting pending changes.
        var row = await settings.GetAsync(ct) ?? new PlatformSettings();
        row.AppliedProxyHash = desired.Fingerprint();
        row.ProxyAppliedAt = clock.GetUtcNow();
        await settings.SaveAsync(row, ct);

        return await backend.GetStatusAsync(deployment, ct);
    }

    // Swarm mounts secrets by name under /run/secrets rather than at an arbitrary path, so the file name
    // is what the config has to point at. Content-hashed object names change per renewal; these don't,
    // which keeps the nginx config itself stable across one.
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
        // External routes need no workload; workload routes only survive if theirs is still a container.
        return (all.Where(r => r.Enabled
            && (r.Target == RouteTarget.External || (r.WorkloadId is not null && names.ContainsKey(r.WorkloadId))))
            .ToList(), names);
    }

    private static List<RouteConfig> ToConfigs(IEnumerable<Route> routable, IReadOnlyDictionary<string, string> names) =>
        routable.Select(r => new RouteConfig
        {
            Hostname = r.Hostname,
            Path = r.Path,
            // Workload routes proxy to the namespaced swarm service, not the workload's own name.
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
            // With no record of an apply, there's drift only if there's something to serve — an empty
            // platform shouldn't nag about an edge nobody needs yet.
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

    /// Container workloads only. nginx proxies to a swarm service name, so a route to anything else has
    /// no resolvable upstream — and nginx refuses to start on one bad upstream, taking every site with it.
    private async Task<Dictionary<string, string>> WorkloadNamesAsync(CancellationToken ct)
        => (await workloads.ListAsync(ct))
            .Where(w => w.Kind == WorkloadKind.Container)
            .ToDictionary(w => w.Id, w => w.Name);

    /// Returns the validated (workloadId, upstreamHost) pair — exactly one is set, per the target.
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

    /// A bare host or IP. It goes straight into proxy_pass, so anything that could carry a scheme, a
    /// path, a port or a second directive is rejected rather than escaped.
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
