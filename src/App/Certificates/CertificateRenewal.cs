using FifthBox.ServerManager.Shared.Certificates;

namespace FifthBox.ServerManager.App.Certificates;

public static class CertificateRenewal
{
    /// pending and failed retry every tick, usually it's dns not pointing here yet
    public static bool IsDue(Certificate certificate, DateTimeOffset now, int renewBeforeDays)
        => certificate.Status != CertificateStatus.Valid
           || certificate.NotAfter is not { } expiry
           || expiry - now < TimeSpan.FromDays(renewBeforeDays);
}
