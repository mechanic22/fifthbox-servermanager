using FifthBox.ServerManager.Shared.Certificates;

namespace FifthBox.ServerManager.App.Certificates;

public static class CertificateRenewal
{
    /// Rows the renewal pass should request. Pending and Failed are retried every tick — the usual
    /// cause is DNS that isn't pointing here yet, which fixes itself without anyone touching this app.
    public static bool IsDue(Certificate certificate, DateTimeOffset now, int renewBeforeDays)
        => certificate.Status != CertificateStatus.Valid
           || certificate.NotAfter is not { } expiry
           || expiry - now < TimeSpan.FromDays(renewBeforeDays);
}
