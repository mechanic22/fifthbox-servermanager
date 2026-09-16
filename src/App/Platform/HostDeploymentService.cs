using FifthBox.ServerManager.Shared.Platform;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Platform;

/// named for the process to dodge Hosting.IHostEnvironment
public interface IHostProcessInfo
{
    /// set by swarm from a templated env var, null outside a service task
    string? SwarmServiceName { get; }

    bool IsContainer { get; }
}

public interface IHostDeploymentService
{
    Task<HostDeploymentInfo> GetAsync(CancellationToken ct = default);
}

public sealed class HostDeploymentService(
    IHostProcessInfo process,
    IPlatformServices platformServices,
    ISecretCustodyService secrets,
    IOptions<HostDeploymentOptions> options,
    IOptions<EncryptionOptions> encryption) : IHostDeploymentService
{
    private readonly HostDeploymentOptions _options = options.Value;

    public async Task<HostDeploymentInfo> GetAsync(CancellationToken ct = default)
    {
        var serviceName = process.SwarmServiceName;
        var runMode = serviceName is not null ? HostRunMode.SwarmService
            : process.IsContainer ? HostRunMode.Container
            : HostRunMode.BareProcess;

        var managedExists = await platformServices.ServiceExistsAsync(_options.ServiceName, ct);

        return new HostDeploymentInfo
        {
            RunMode = runMode,
            ServiceName = serviceName,
            ManagedServiceExists = managedExists,
            // the dangerous one, a managed service and us both running on one socket and db
            SplitBrain = managedExists && runMode != HostRunMode.SwarmService,
            InstallCommand = InstallCommandRenderer.Render(_options, await LocalNodeIdAsync(ct)),
            EncryptionKeyEphemeral = encryption.Value.Ephemeral,
            Secrets = await secrets.CheckAsync(ct),
        };
    }

    private async Task<string?> LocalNodeIdAsync(CancellationToken ct)
    {
        try
        {
            return await platformServices.LocalNodeIdAsync(ct);
        }
        catch (Exception)
        {
            // no swarm yet is normal pre-bootstrap, the command falls back to a role constraint
            return null;
        }
    }
}
