using FifthBox.ServerManager.App.Agents;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class AgentSecretsTests
{
    [TestMethod]
    public void Generate_produces_distinct_high_entropy_tokens()
    {
        var a = AgentSecrets.Generate();
        var b = AgentSecrets.Generate();

        Assert.AreNotEqual(a, b);
        Assert.IsGreaterThanOrEqualTo(40, a.Length);
    }

    [TestMethod]
    public void Verify_is_true_for_the_matching_secret()
    {
        var secret = AgentSecrets.Generate();
        Assert.IsTrue(AgentSecrets.Verify(secret, AgentSecrets.Hash(secret)));
    }

    [TestMethod]
    public void Verify_is_false_for_a_wrong_secret()
    {
        var hash = AgentSecrets.Hash(AgentSecrets.Generate());
        Assert.IsFalse(AgentSecrets.Verify(AgentSecrets.Generate(), hash));
    }

    [TestMethod]
    public void Verify_is_false_for_missing_or_garbage_hash()
    {
        Assert.IsFalse(AgentSecrets.Verify("x", null));
        Assert.IsFalse(AgentSecrets.Verify("x", ""));
        Assert.IsFalse(AgentSecrets.Verify("x", "not-valid-base64!!"));
    }

    [TestMethod]
    public void Hash_does_not_contain_the_secret()
    {
        var secret = AgentSecrets.Generate();
        Assert.DoesNotContain(secret, AgentSecrets.Hash(secret));
    }
}
