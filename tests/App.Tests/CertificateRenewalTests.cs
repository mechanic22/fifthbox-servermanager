using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.Shared.Certificates;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class CertificateRenewalTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    private static Certificate Valid(TimeSpan expiresIn) =>
        new() { Status = CertificateStatus.Valid, NotAfter = Now + expiresIn };

    [TestMethod]
    public void Pending_and_failed_are_retried_every_pass()
    {
        Assert.IsTrue(CertificateRenewal.IsDue(new Certificate { Status = CertificateStatus.Pending }, Now, 30));
        Assert.IsTrue(CertificateRenewal.IsDue(new Certificate { Status = CertificateStatus.Failed }, Now, 30));
    }

    [TestMethod]
    public void Valid_is_due_only_inside_the_renewal_window()
    {
        Assert.IsFalse(CertificateRenewal.IsDue(Valid(TimeSpan.FromDays(31)), Now, 30));
        Assert.IsTrue(CertificateRenewal.IsDue(Valid(TimeSpan.FromDays(29)), Now, 30));
    }

    [TestMethod]
    public void An_expired_certificate_is_due()
    {
        Assert.IsTrue(CertificateRenewal.IsDue(Valid(TimeSpan.FromDays(-1)), Now, 30));
    }

    [TestMethod]
    public void A_valid_row_with_no_expiry_is_due_rather_than_ignored()
    {
        // Nothing should produce this, but treating unknown as "never renew" would silently strand it.
        Assert.IsTrue(CertificateRenewal.IsDue(new Certificate { Status = CertificateStatus.Valid }, Now, 30));
    }
}
