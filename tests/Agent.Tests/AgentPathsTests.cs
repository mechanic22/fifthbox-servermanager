namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class AgentPathsTests
{
    private static readonly string Base = Path.Combine(Path.DirectorySeparatorChar.ToString(), "opt", "fbsm-agent");

    [TestMethod]
    public void A_relative_path_lands_next_to_the_executable()
    {
        // as a windows service cwd is System32, anchor to the exe or creds move and it re-enrolls every restart
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
