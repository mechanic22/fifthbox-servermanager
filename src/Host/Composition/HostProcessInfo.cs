using FifthBox.ServerManager.App.Platform;

namespace FifthBox.ServerManager.Host.Composition;

public sealed class HostProcessInfo : IHostProcessInfo
{
    /// Populated from `--env FBSM_SERVICE_NAME='{{.Service.Name}}'`, which swarm expands per task. It
    /// can only be present when running as a service, so no container introspection is needed.
    public string? SwarmServiceName
    {
        get
        {
            var name = Environment.GetEnvironmentVariable("FBSM_SERVICE_NAME");
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }

    public bool IsContainer => File.Exists("/.dockerenv");
}
