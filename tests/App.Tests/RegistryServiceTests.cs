using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Registries;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class RegistryServiceTests
{
    // A reversible fake protector: "enc(<plain>)". Proves the service encrypts before storing and
    // decrypts only when resolving.
    private sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"enc({plaintext})";
        public string Unprotect(string ciphertext) => ciphertext[4..^1];
    }

    private static (RegistryService svc, Mock<IRegistryRepository> repo) Build(params Registry[] existing)
    {
        var repo = new Mock<IRegistryRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing.ToList());
        repo.Setup(r => r.AddAsync(It.IsAny<Registry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return (new RegistryService(repo.Object, new FakeProtector(), TimeProvider.System), repo);
    }

    [TestMethod]
    public async Task Create_encrypts_the_password_and_never_returns_it()
    {
        var (svc, repo) = Build();
        Registry? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Registry>(), It.IsAny<CancellationToken>()))
            .Callback<Registry, CancellationToken>((r, _) => saved = r).Returns(Task.CompletedTask);

        var result = await svc.CreateAsync(new CreateRegistryRequest
        {
            Domain = "docker.example.com", Username = "tgilbert", Password = "s3cret", Prefix = "example/",
        });

        Assert.AreEqual("enc(s3cret)", saved!.PasswordEnc);   // protector output, not the raw password
        Assert.AreEqual("docker.example.com", result.Domain);
        Assert.AreEqual("tgilbert", result.Username);
        // RegistryResponse has no password member — nothing to leak.
    }

    [TestMethod]
    public async Task Create_without_password_throws_validation()
    {
        var (svc, _) = Build();
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            svc.CreateAsync(new CreateRegistryRequest { Domain = "d", Username = "u" }));
    }

    [TestMethod]
    public async Task Resolve_matches_by_domain_and_decrypts()
    {
        var (svc, _) = Build(new Registry { Domain = "docker.example.com", Username = "u", PasswordEnc = "enc(pw)" });

        var auth = await svc.ResolveAsync("docker.example.com/demo:latest");

        Assert.IsNotNull(auth);
        Assert.AreEqual("u", auth!.Username);
        Assert.AreEqual("pw", auth.Password);   // decrypted just-in-time
        Assert.AreEqual("docker.example.com", auth.ServerAddress);
    }

    [TestMethod]
    public async Task Resolve_returns_null_when_no_registry_matches()
    {
        var (svc, _) = Build(new Registry { Domain = "docker.example.com", Username = "u", PasswordEnc = "enc(pw)" });

        Assert.IsNull(await svc.ResolveAsync("docker.io/library/nginx:latest"));
    }
}
