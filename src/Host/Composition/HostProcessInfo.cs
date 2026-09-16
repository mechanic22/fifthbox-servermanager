using FifthBox.ServerManager.App.Platform;

namespace FifthBox.ServerManager.Host.Composition;

public sealed class HostProcessInfo : IHostProcessInfo
{
    /// from --env FBSM_SERVICE_NAME='{{.Service.Name}}', swarm fills it in per task. null outside swarm
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
