using System.Security.Cryptography;
using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.Shared.Certificates;
using FifthBox.ServerManager.Shared.Exceptions;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class CertificateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    private static CertificateService Build(
        Mock<ICertificateRepository> certificates,
        Mock<IAcmeClient>? acme = null,
        Mock<IRouteRepository>? routes = null,
        int renewBeforeDays = 30)
    {
        var routeRepo = routes ?? WithRoute();
        var client = acme ?? Issuing();
        return new CertificateService(
            certificates.Object,
            routeRepo.Object,
            client.Object,
            new PassthroughProtector(),
            Options.Create(new AcmeOptions { RenewBeforeDays = renewBeforeDays }),
            new FixedClock(Now));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// refuses anything it didn't produce, like a db restored without its key
    private sealed class PassthroughProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"enc:{plaintext}";

        public string Unprotect(string ciphertext) => ciphertext.StartsWith("enc:", StringComparison.Ordinal)
            ? ciphertext["enc:".Length..]
            : throw new CryptographicException("The key does not open this value.");
    }

    private static Mock<IRouteRepository> WithRoute(string hostname = "app.example.com")
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Route { Hostname = hostname, Path = "/" }]);
        return routes;
    }

    private static Mock<IAcmeClient> Issuing(DateTimeOffset? notAfter = null)
    {
        var acme = new Mock<IAcmeClient>();
        acme.Setup(a => a.UsesProductionCa).Returns(true);
        acme.Setup(a => a.IssueAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssuedCertificate
            {
                PemChain = "-----BEGIN CERTIFICATE-----leaf",
                PrivateKeyPem = "-----BEGIN EC PRIVATE KEY-----secret",
                NotBefore = Now,
                NotAfter = notAfter ?? Now.AddDays(90),
            });
        return acme;
    }

    private static Mock<IAcmeClient> Failing(string message = "DNS problem: NXDOMAIN")
    {
        var acme = new Mock<IAcmeClient>();
        acme.Setup(a => a.IssueAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CertificateIssuanceException(message));
        return acme;
    }

    private static Mock<ICertificateRepository> Repo(params Certificate[] existing)
    {
        var repo = new Mock<ICertificateRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        foreach (var certificate in existing)
        {
            repo.Setup(r => r.FindByHostnameAsync(certificate.Hostname, It.IsAny<CancellationToken>()))
                .ReturnsAsync(certificate);
        }

        return repo;
    }

    [TestMethod]
    public async Task Enable_creates_the_row_and_issues_immediately()
    {
        var repo = Repo();
        Certificate? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Certificate>(), It.IsAny<CancellationToken>()))
            .Callback<Certificate, CancellationToken>((c, _) => saved = c).Returns(Task.CompletedTask);

        var result = await Build(repo).EnableAsync("App.Example.com");

        Assert.AreEqual("app.example.com", saved!.Hostname, "hostnames are matched against routes lowercased");
        Assert.AreEqual(CertificateStatus.Valid, result.Status);
        Assert.AreEqual(Now.AddDays(90), result.NotAfter);
        Assert.IsNull(result.LastError);
        Assert.AreEqual("enc:-----BEGIN EC PRIVATE KEY-----secret", saved.PrivateKeyEnc, "the key is encrypted at rest");
    }

    [TestMethod]
    public async Task Enable_for_a_hostname_with_no_route_throws_and_saves_nothing()
    {
        var repo = Repo();
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // HTTP-01 fetches from the hostname, so nginx has to be serving it already
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(repo, routes: routes).EnableAsync("app.example.com"));

        repo.Verify(r => r.AddAsync(It.IsAny<Certificate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Enable_for_a_reserved_hostname_throws_before_any_order_is_placed()
    {
        var repo = Repo();
        var acme = Issuing();

        var thrown = await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(repo, acme, WithRoute("dev-01.local")).EnableAsync("dev-01.local"));

        // the CA would refuse it anyway and leave a Failed row and a burned order
        StringAssert.Contains(thrown.Errors[nameof(EnableHttpsRequest.Hostname)].Single(), ".local");
        repo.Verify(r => r.AddAsync(It.IsAny<Certificate>(), It.IsAny<CancellationToken>()), Times.Never);
        acme.Verify(a => a.IssueAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Overview_says_why_a_reserved_hostname_cannot_have_https()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Route { Hostname = "dev-01.local", Path = "/" },
            new Route { Hostname = "app.example.com", Path = "/" },
        ]);

        var overview = await Build(Repo(), routes: routes).OverviewAsync();

        // the page offers Enable off this flag, so say so up front instead of failing after the click
        StringAssert.Contains(overview.Hosts.Single(h => h.Hostname == "dev-01.local").IssuanceBlockedReason, ".local");
        Assert.IsNull(overview.Hosts.Single(h => h.Hostname == "app.example.com").IssuanceBlockedReason);
    }

    [TestMethod]
    public async Task Enable_twice_conflicts()
    {
        var repo = Repo(new Certificate { Hostname = "app.example.com" });

        await Assert.ThrowsExactlyAsync<ConflictException>(() => Build(repo).EnableAsync("app.example.com"));
    }

    [TestMethod]
    public async Task A_first_issuance_that_fails_records_the_reason()
    {
        var repo = Repo();
        Certificate? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Certificate>(), It.IsAny<CancellationToken>()))
            .Callback<Certificate, CancellationToken>((c, _) => saved = c).Returns(Task.CompletedTask);

        var result = await Build(repo, Failing()).EnableAsync("app.example.com");

        Assert.AreEqual(CertificateStatus.Failed, result.Status);
        StringAssert.Contains(result.LastError, "NXDOMAIN");
        Assert.IsNull(saved!.PemChain);
    }

    [TestMethod]
    public async Task A_failed_renewal_leaves_a_still_valid_certificate_serving()
    {
        var existing = Live("app.example.com", expiresIn: TimeSpan.FromDays(20));
        var repo = Repo(existing);

        var changed = await Build(repo, Failing("connection refused")).IssueDueAsync();

        // ~20 days of retries left, dropping to http over one flaky CA call is a self-inflicted outage
        Assert.AreEqual(0, changed);
        Assert.AreEqual(CertificateStatus.Valid, existing.Status);
        Assert.AreEqual("-----BEGIN CERTIFICATE-----old", existing.PemChain);
        StringAssert.Contains(existing.LastError, "connection refused");
    }

    [TestMethod]
    public async Task A_failed_renewal_after_expiry_marks_it_failed()
    {
        var expired = Live("app.example.com", expiresIn: TimeSpan.FromDays(-1));
        var repo = Repo(expired);

        await Build(repo, Failing()).IssueDueAsync();

        // generator only emits ssl_certificate for Valid rows, so this drops to plain http instead of a bad cert
        Assert.AreEqual(CertificateStatus.Failed, expired.Status);
    }

    [TestMethod]
    public async Task Renewal_replaces_a_certificate_inside_the_window()
    {
        var existing = Live("app.example.com", expiresIn: TimeSpan.FromDays(20));
        var repo = Repo(existing);

        var changed = await Build(repo).IssueDueAsync();

        Assert.AreEqual(1, changed, "the caller reapplies nginx when something changed");
        Assert.AreEqual("-----BEGIN CERTIFICATE-----leaf", existing.PemChain);
        Assert.AreEqual(Now.AddDays(90), existing.NotAfter);
    }

    [TestMethod]
    public async Task Renewal_leaves_certificates_outside_the_window_alone()
    {
        var comfortable = Live("app.example.com", expiresIn: TimeSpan.FromDays(60));
        var repo = Repo(comfortable);
        var acme = Issuing();

        var changed = await Build(repo, acme).IssueDueAsync();

        Assert.AreEqual(0, changed);
        acme.Verify(a => a.IssueAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Renewal_never_creates_a_row()
    {
        var repo = Repo();

        await Build(repo).IssueDueAsync();

        // enabling https is always the operator's call, a job doing it would issue for hosts nobody asked about
        repo.Verify(r => r.AddAsync(It.IsAny<Certificate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Disable_removes_the_row()
    {
        var existing = Live("app.example.com", expiresIn: TimeSpan.FromDays(60));
        var repo = Repo(existing);

        await Build(repo).DisableAsync("app.example.com");

        repo.Verify(r => r.RemoveAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Disable_for_a_hostname_without_https_throws_not_found()
    {
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => Build(Repo()).DisableAsync("nope.example.com"));
    }

    [TestMethod]
    public async Task Overview_lists_routed_hostnames_that_have_no_certificate_yet()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Route { Hostname = "plain.example.com", Path = "/" },
            new Route { Hostname = "app.example.com", Path = "/" },
            new Route { Hostname = "app.example.com", Path = "/admin" },
        ]);
        var repo = Repo(Live("app.example.com", expiresIn: TimeSpan.FromDays(60)));

        var overview = await Build(repo, routes: routes).OverviewAsync();

        // hosts without https are what you'd enable, so they have to be listed
        CollectionAssert.AreEqual(
            new[] { "app.example.com", "plain.example.com" },
            overview.Hosts.Select(h => h.Hostname).ToArray());
        Assert.IsNull(overview.Hosts.Single(h => h.Hostname == "plain.example.com").Status);
        Assert.AreEqual(2, overview.Hosts.Single(h => h.Hostname == "app.example.com").RouteCount,
            "disabling affects every route on the hostname, so the count is worth showing");
    }

    [TestMethod]
    public async Task Overview_keeps_a_certificate_whose_routes_have_all_been_deleted()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var repo = Repo(Live("orphan.example.com", expiresIn: TimeSpan.FromDays(60)));

        var overview = await Build(repo, routes: routes).OverviewAsync();

        // otherwise it's a private key nobody can see or delete
        Assert.AreEqual("orphan.example.com", overview.Hosts.Single().Hostname);
        Assert.AreEqual(0, overview.Hosts.Single().RouteCount);
    }

    [TestMethod]
    public async Task Overview_flags_the_staging_ca()
    {
        var staging = new Mock<IAcmeClient>();
        staging.Setup(a => a.UsesProductionCa).Returns(false);

        Assert.IsTrue((await Build(Repo(), staging).OverviewAsync()).StagingCa);
    }

    [TestMethod]
    public async Task Retry_reissues_one_hostname_now()
    {
        var failed = new Certificate { Hostname = "app.example.com", Status = CertificateStatus.Failed, LastError = "NXDOMAIN" };
        var repo = Repo(failed);

        var result = await Build(repo).RetryAsync("app.example.com");

        Assert.AreEqual(CertificateStatus.Valid, result.Status);
        Assert.IsNull(failed.LastError);
    }

    [TestMethod]
    public async Task Retry_for_a_hostname_without_https_throws_not_found()
    {
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => Build(Repo()).RetryAsync("nope.example.com"));
    }

    [TestMethod]
    public async Task Installable_returns_valid_certificates_with_the_key_decrypted()
    {
        var repo = Repo(Live("app.example.com", expiresIn: TimeSpan.FromDays(60)));

        var installable = await Build(repo).InstallableAsync();

        Assert.AreEqual("app.example.com", installable.Single().Hostname);
        Assert.AreEqual("old-key", installable.Single().PrivateKeyPem);
    }

    [TestMethod]
    public async Task Installable_skips_pending_failed_and_expired_rows()
    {
        var expired = Live("expired.example.com", expiresIn: TimeSpan.FromDays(-1));
        var repo = Repo(
            new Certificate { Hostname = "pending.example.com" },
            new Certificate { Hostname = "failed.example.com", Status = CertificateStatus.Failed },
            expired);

        var installable = await Build(repo).InstallableAsync();

        // an expired row can still say Valid if nothing retried it (host was off), http beats a cert error
        Assert.IsEmpty(installable);
    }

    [TestMethod]
    public async Task Installable_skips_a_key_the_current_encryption_key_cannot_open()
    {
        var good = Live("good.example.com", expiresIn: TimeSpan.FromDays(60));
        var restored = Live("restored.example.com", expiresIn: TimeSpan.FromDays(60));
        restored.PrivateKeyEnc = "not-openable-with-this-key";
        var repo = Repo(good, restored);

        var installable = await Build(repo).InstallableAsync();

        // restored db with a different encryption key, skip that one so the rest of the edge still deploys
        Assert.AreEqual("good.example.com", installable.Single().Hostname);
    }

    private static Certificate Live(string hostname, TimeSpan expiresIn) => new()
    {
        Hostname = hostname,
        Status = CertificateStatus.Valid,
        PemChain = "-----BEGIN CERTIFICATE-----old",
        PrivateKeyEnc = "enc:old-key",
        IssuedAt = Now - TimeSpan.FromDays(90) + expiresIn,
        NotAfter = Now + expiresIn,
    };
}
