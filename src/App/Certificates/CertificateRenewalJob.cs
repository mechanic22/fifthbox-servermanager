using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.App.Routes;

namespace FifthBox.ServerManager.App.Certificates;

/// Daily is enough: 90-day certificates renewed with 30 days left give ~30 attempts before it matters,
/// so a missed tick or a transient CA failure costs one chance out of thirty.
public sealed class CertificateRenewalJob(ICertificateService certificates, IRouteService routes) : IScheduledJob
{
    public string Name => "certificates";

    public TimeSpan Interval => TimeSpan.FromHours(24);

    public async Task RunAsync(CancellationToken ct = default)
    {
        // A new certificate is only serving once nginx is holding it, so a renewal that doesn't reapply
        // has done nothing a visitor can see.
        if (await certificates.IssueDueAsync(ct) > 0)
        {
            await routes.ApplyAsync(ct);
        }
    }
}
