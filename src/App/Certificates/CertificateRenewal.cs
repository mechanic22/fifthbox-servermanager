using FifthBox.ServerManager.Shared.Certificates;

namespace FifthBox.ServerManager.App.Certificates;

public static class CertificateRenewal
{
    /// pending and failed retry every tick, usually it's dns not pointing here yet
    /// a www toggle the stored cert doesn't match yet also counts, so a failed reissue keeps retrying
    public static bool IsDue(Certificate certificate, DateTimeOffset now, int renewBeforeDays, bool wantsWww = false)
        => certificate.Status != CertificateStatus.Valid
           || certificate.IncludesWww != wantsWww
           || certificate.NotAfter is not { } expiry
           || expiry - now < TimeSpan.FromDays(renewBeforeDays);
}
