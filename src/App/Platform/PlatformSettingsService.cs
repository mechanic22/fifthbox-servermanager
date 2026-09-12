using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;
using FifthBox.ServerManager.Shared.Routes;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Platform;

public interface IPlatformSettingsService
{
    Task<PlatformSettingsResponse> GetAsync(CancellationToken ct = default);
    Task<PlatformSettingsResponse> UpdateAsync(UpdatePlatformSettingsRequest request, CancellationToken ct = default);
}

public sealed class PlatformSettingsService(
    IPlatformSettingsRepository repository,
    IRouteService routes,
    IOptions<ReverseProxyOptions> proxyOptions,
    IOptions<HostDeploymentOptions> hostOptions,
    TimeProvider clock) : IPlatformSettingsService
{
    private readonly ReverseProxyOptions _proxy = proxyOptions.Value;
    private readonly HostDeploymentOptions _host = hostOptions.Value;

    public async Task<PlatformSettingsResponse> GetAsync(CancellationToken ct = default)
        => Map(await repository.GetAsync(ct) ?? new PlatformSettings());

    public async Task<PlatformSettingsResponse> UpdateAsync(UpdatePlatformSettingsRequest request, CancellationToken ct = default)
    {
        var email = Blank(request.AcmeEmail);
        if (email is not null && !email.Contains('@'))
        {
            throw new ValidationException(nameof(UpdatePlatformSettingsRequest.AcmeEmail), "Enter a valid email address.");
        }

        var settings = await repository.GetAsync(ct) ?? new PlatformSettings();
        var before = ManagerHostname(settings);

        settings.RootDomain = Blank(request.RootDomain)?.ToLowerInvariant();
        settings.AcmeEmail = email;
        settings.ManagerPrefix = ValidateManagerPrefix(request.ManagerPrefix);
        settings.UpdatedAt = clock.GetUtcNow();

        await repository.SaveAsync(settings, ct);

        // Only when the address actually changes. A previous one is left in place rather than deleted:
        // removing a route here would silently take its certificate with it.
        if (ManagerHostname(settings) is { } hostname && hostname != before)
        {
            await ProvisionManagerRouteAsync(hostname, ct);
        }

        return Map(settings);
    }

    /// Gives the manager itself a hostname under the root domain, so it can be reached over TLS like
    /// anything else the platform serves rather than on its published port.
    private async Task ProvisionManagerRouteAsync(string hostname, CancellationToken ct)
    {
        try
        {
            await routes.CreateAsync(new CreateRouteRequest
            {
                Hostname = hostname,
                Path = "/",
                Target = RouteTarget.External,
                // The Host's own swarm service, resolved by Docker DNS over the overlay — the same way
                // the ACME challenge location reaches it.
                UpstreamHost = _host.ServiceName,
                TargetPort = _host.ContainerPort,
                // Three SignalR hubs ride this route; without the upgrade headers live status and log
                // streaming stop working, and nothing about the page looks broken.
                WebSockets = true,
            }, ct);
        }
        catch (ConflictException)
        {
            // Something already answers on that hostname — leave it alone.
        }
    }

    private static string? ManagerHostname(PlatformSettings settings)
        => Blank(settings.ManagerPrefix) is { } prefix
            ? DefaultHostname.For(prefix, settings.RootDomain)
            : null;

    private static string? ValidateManagerPrefix(string? prefix)
    {
        var value = Blank(prefix)?.ToLowerInvariant();
        if (value is null)
        {
            return null;
        }

        // One label, so it stays inside a *.<root domain> wildcard — a dotted prefix would need its own
        // certificate and its own DNS record.
        if (!value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new ValidationException(nameof(UpdatePlatformSettingsRequest.ManagerPrefix),
                "Use a single label — letters, digits and hyphens only.");
        }

        return value;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private PlatformSettingsResponse Map(PlatformSettings s) => new()
    {
        RootDomain = s.RootDomain,
        AcmeEmail = s.AcmeEmail,
        ManagerPrefix = s.ManagerPrefix,
        WorkloadNamePrefix = SwarmNaming.WorkloadPrefix,
        EdgeHttpPort = _proxy.HttpPort,
        EdgeHttpsPort = _proxy.HttpsPort,
    };
}
