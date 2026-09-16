using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.App.Routes;

namespace FifthBox.ServerManager.App.Certificates;

/// daily is plenty, ~30 attempts before a cert expires
public sealed class CertificateRenewalJob(ICertificateService certificates, IRouteService routes) : IScheduledJob
{
    public string Name => "certificates";

    public TimeSpan Interval => TimeSpan.FromHours(24);

    public async Task RunAsync(CancellationToken ct = default)
    {
        // a new cert isn't serving until nginx has it
        if (await certificates.IssueDueAsync(ct) > 0)
        {
            await routes.ApplyAsync(ct);
        }
    }
}
