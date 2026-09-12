using FifthBox.ServerManager.Agent;

namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class InstallPathsTests
{
    private static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fbsm-agent-root"));

    private static string InstallRoot
    {
        get
        {
            Assert.IsTrue(InstallPaths.TryRootFor(Root, "csgo", out var root));
            return root;
        }
    }

    [TestMethod]
    public void A_workload_gets_a_directory_under_the_agent_root()
    {
        Assert.IsTrue(InstallPaths.TryRootFor(Root, "csgo", out var root));
        Assert.AreEqual(Path.Combine(Root, "csgo"), root);
    }

    [TestMethod]
    [DataRow("..")]
    [DataRow(".")]
    [DataRow("../escape")]
    [DataRow("..\\escape")]
    [DataRow("sub/dir")]
    [DataRow("sub\\dir")]
    [DataRow("")]
    [DataRow("   ")]
    public void A_name_that_is_not_one_plain_segment_gets_no_directory(string name)
    {
        Assert.IsFalse(InstallPaths.TryRootFor(Root, name, out var root));
        Assert.AreEqual(string.Empty, root);
    }

    [TestMethod]
    public void A_relative_path_resolves_inside_the_install_root()
    {
        Assert.IsTrue(InstallPaths.TryResolveWithin(InstallRoot, "cfg/server.cfg", out var full));
        Assert.AreEqual(Path.Combine(InstallRoot, "cfg", "server.cfg"), full);
    }

    [TestMethod]
    public void Redundant_segments_that_stay_inside_are_fine()
    {
        Assert.IsTrue(InstallPaths.TryResolveWithin(InstallRoot, "cfg/../cfg/server.cfg", out var full));
        Assert.AreEqual(Path.Combine(InstallRoot, "cfg", "server.cfg"), full);
    }

    [TestMethod]
    [DataRow("../../windows/system32/cmd.exe")]
    [DataRow("..")]
    [DataRow("cfg/../../../etc/passwd")]
    [DataRow("")]
    public void A_path_that_climbs_out_is_refused(string relative)
    {
        Assert.IsFalse(InstallPaths.TryResolveWithin(InstallRoot, relative, out var full));
        Assert.AreEqual(string.Empty, full);
    }

    [TestMethod]
    public void An_absolute_path_is_refused_rather_than_silently_replacing_the_root()
    {
        // Path.Combine would discard the root here, so this must be caught before containment is checked.
        var absolute = OperatingSystem.IsWindows() ? @"C:\windows\system32\cmd.exe" : "/etc/passwd";

        Assert.IsFalse(InstallPaths.TryResolveWithin(InstallRoot, absolute, out _));
    }

    [TestMethod]
    public void A_sibling_directory_sharing_the_root_prefix_is_still_outside()
    {
        // "…/csgo-evil" starts with "…/csgo" as a string but is not inside it.
        Assert.IsTrue(InstallPaths.TryRootFor(Root, "csgo-evil", out var sibling));
        Assert.IsFalse(sibling.StartsWith(InstallRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        Assert.IsFalse(InstallPaths.TryResolveWithin(InstallRoot, "../csgo-evil/run.sh", out _));
    }

    [TestMethod]
    public void A_bare_command_is_left_for_the_OS_to_find_on_PATH()
    {
        Assert.IsTrue(InstallPaths.TryResolveCommand(InstallRoot, "java", out var resolved));
        Assert.AreEqual("java", resolved, "resolving this against the install root would break PATH lookup");
    }

    [TestMethod]
    public void An_absolute_command_is_the_operators_own_choice()
    {
        var absolute = OperatingSystem.IsWindows() ? @"C:\Program Files\Java\bin\java.exe" : "/usr/bin/java";

        Assert.IsTrue(InstallPaths.TryResolveCommand(InstallRoot, absolute, out var resolved));
        Assert.AreEqual(absolute, resolved);
    }

    [TestMethod]
    public void A_relative_command_resolves_against_the_install_root()
    {
        Assert.IsTrue(InstallPaths.TryResolveCommand(InstallRoot, "./srcds_run", out var resolved));
        Assert.AreEqual(Path.Combine(InstallRoot, "srcds_run"), resolved);
    }

    [TestMethod]
    public void A_relative_command_may_not_climb_out_of_the_install_root()
    {
        Assert.IsFalse(InstallPaths.TryResolveCommand(InstallRoot, "../../evil.sh", out var resolved));
        Assert.AreEqual(string.Empty, resolved);
    }
}
