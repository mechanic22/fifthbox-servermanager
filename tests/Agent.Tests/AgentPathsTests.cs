namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class AgentPathsTests
{
    private static readonly string Base = Path.Combine(Path.DirectorySeparatorChar.ToString(), "opt", "fbsm-agent");

    [TestMethod]
    public void A_relative_path_lands_next_to_the_executable()
    {
        // As a Windows service the working directory is System32; anchoring to the exe is what keeps the
        // credential file where the operator unzipped it — and stops a re-enroll on every restart.
        Assert.AreEqual(
            Path.Combine(Base, "agent-credentials.json"),
            AgentPaths.Resolve("agent-credentials.json", Base));
    }

    [TestMethod]
    public void An_absolute_path_is_left_alone()
    {
        var absolute = Path.Combine(Path.DirectorySeparatorChar.ToString(), "var", "lib", "fbsm", "creds.json");

        Assert.AreEqual(absolute, AgentPaths.Resolve(absolute, Base));
    }

    [DataRow("")]
    [DataRow("   ")]
    [TestMethod]
    public void A_blank_path_is_passed_through_rather_than_becoming_the_base_directory(string path)
    {
        Assert.AreEqual(path, AgentPaths.Resolve(path, Base));
    }
}
