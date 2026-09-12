using FifthBox.ServerManager.Shared.Platform;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Platform;

/// What the Host process can observe about itself. Implemented in the Host — only it knows its own
/// environment. Named for the process, not the environment, to stay clear of Hosting.IHostEnvironment.
public interface IHostProcessInfo
{
    /// Set by swarm from a templated service-spec env var; null for anything not a service task.
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
            // The dangerous state: a managed service is running and so are we, separately. Nothing else
            // surfaces two Hosts sharing a Docker socket and a database.
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
            // No swarm yet is the normal case before bootstrap — the command falls back to a role
            // constraint rather than failing the whole page.
            return null;
        }
    }
}
