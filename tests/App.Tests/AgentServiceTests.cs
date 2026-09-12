using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Exceptions;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class AgentServiceTests
{
    private static (AgentService svc, Mock<IAgentRepository> agents, Mock<IEnrollmentKeyStore> keys, Mock<IAgentRegistry> registry) Build()
    {
        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var keys = new Mock<IEnrollmentKeyStore>();
        var registry = new Mock<IAgentRegistry>();
        return (new AgentService(agents.Object, keys.Object, registry.Object, TimeProvider.System), agents, keys, registry);
    }

    [TestMethod]
    public async Task GenerateEnrollmentKey_stores_hash_and_returns_plaintext_once()
    {
        var (svc, _, keys, _) = Build();
        string? storedHash = null;
        keys.Setup(k => k.SetHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((h, _) => storedHash = h).Returns(Task.CompletedTask);

        var result = await svc.GenerateEnrollmentKeyAsync();

        Assert.IsFalse(string.IsNullOrEmpty(result.Key));
        Assert.AreNotEqual(result.Key, storedHash);          // stored the hash, not the key
        Assert.IsTrue(AgentSecrets.Verify(result.Key, storedHash));
    }

    [TestMethod]
    public async Task Enroll_with_valid_key_creates_agent_and_returns_credential()
    {
        var (svc, agents, keys, _) = Build();
        const string enrollmentKey = "the-key";
        keys.Setup(k => k.GetHashAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AgentSecrets.Hash(enrollmentKey));
        Agent? saved = null;
        agents.Setup(a => a.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Callback<Agent, CancellationToken>((a, _) => saved = a).Returns(Task.CompletedTask);

        var result = await svc.EnrollAsync(new EnrollAgentRequest { EnrollmentKey = enrollmentKey, Name = "win-1", Platform = AgentPlatform.Windows });

        Assert.IsFalse(string.IsNullOrEmpty(result.Secret));
        Assert.AreEqual(saved!.Id, result.AgentId);
        Assert.AreEqual("win-1", saved.Name);
        Assert.AreEqual(AgentPlatform.Windows, saved.Platform);
        Assert.AreNotEqual(result.Secret, saved.SecretHash);              // stored hash, not plaintext
        Assert.IsTrue(AgentSecrets.Verify(result.Secret, saved.SecretHash));
    }

    [TestMethod]
    public async Task Enroll_with_wrong_key_throws_unauthorized()
    {
        var (svc, _, keys, _) = Build();
        keys.Setup(k => k.GetHashAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AgentSecrets.Hash("real-key"));

        await Assert.ThrowsExactlyAsync<UnauthorizedException>(() =>
            svc.EnrollAsync(new EnrollAgentRequest { EnrollmentKey = "wrong", Name = "x" }));
    }

    [TestMethod]
    public async Task Enroll_when_no_key_configured_throws_unauthorized()
    {
        var (svc, _, keys, _) = Build();
        keys.Setup(k => k.GetHashAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        await Assert.ThrowsExactlyAsync<UnauthorizedException>(() =>
            svc.EnrollAsync(new EnrollAgentRequest { EnrollmentKey = "anything", Name = "x" }));
    }

    [TestMethod]
    public async Task Authenticate_verifies_secret_against_stored_hash()
    {
        var (svc, agents, _, _) = Build();
        var secret = AgentSecrets.Generate();
        agents.Setup(a => a.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Agent { Id = "a1", SecretHash = AgentSecrets.Hash(secret) });

        Assert.IsTrue(await svc.AuthenticateAsync("a1", secret));
        Assert.IsFalse(await svc.AuthenticateAsync("a1", "wrong"));
    }

    [TestMethod]
    public async Task List_reflects_online_status_from_the_registry()
    {
        var (svc, agents, _, registry) = Build();
        agents.Setup(a => a.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { new() { Id = "a1", Name = "on" }, new() { Id = "a2", Name = "off" } });
        registry.Setup(r => r.IsOnline("a1")).Returns(true);
        registry.Setup(r => r.IsOnline("a2")).Returns(false);

        var list = await svc.ListAsync();

        Assert.AreEqual(AgentStatus.Online, list.Single(a => a.Id == "a1").Status);
        Assert.AreEqual(AgentStatus.Offline, list.Single(a => a.Id == "a2").Status);
    }
}
