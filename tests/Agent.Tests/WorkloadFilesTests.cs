using FifthBox.ServerManager.Agent;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class WorkloadFilesTests
{
    private string _root = string.Empty;
    private WorkloadFiles _files = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "fbsm-files-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(_root, "srcds", "cfg"));
        File.WriteAllText(Path.Combine(_root, "srcds", "cfg", "server.cfg"), "hostname \"box\"");
        File.WriteAllText(Path.Combine(_root, "outside.txt"), "not yours");

        _files = new WorkloadFiles(Options.Create(new AgentOptions { RootPath = _root }));
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception)
        {
            // A leftover temp directory isn't worth failing a test over.
        }
    }

    [TestMethod]
    public void Listing_the_root_shows_what_is_in_the_workloads_own_directory()
    {
        var entries = _files.List("srcds", null);

        Assert.HasCount(1, entries);
        Assert.AreEqual("cfg", entries[0].Name);
        Assert.IsTrue(entries[0].IsDirectory);
    }

    [TestMethod]
    public void A_file_reads_back_with_a_forward_slash_path_whatever_the_platform()
    {
        var content = _files.Read("srcds", "cfg/server.cfg");

        Assert.AreEqual("cfg/server.cfg", content.Path);
        StringAssert.Contains(content.Text, "hostname");
    }

    [TestMethod]
    [DataRow("../outside.txt")]
    [DataRow("cfg/../../outside.txt")]
    [DataRow("..")]
    public void A_path_that_climbs_out_of_the_workload_directory_is_refused(string path)
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => _files.Read("srcds", path));
    }

    [TestMethod]
    public void A_binary_file_is_refused_rather_than_mangled_into_text()
    {
        File.WriteAllBytes(Path.Combine(_root, "srcds", "server.bin"), [0x7f, 0x45, 0x00, 0x4c]);

        Assert.ThrowsExactly<InvalidOperationException>(() => _files.Read("srcds", "server.bin"));
    }

    [TestMethod]
    public void Writing_replaces_an_existing_file()
    {
        _files.Write("srcds", "cfg/server.cfg", "hostname \"new\"");

        Assert.AreEqual("hostname \"new\"", File.ReadAllText(Path.Combine(_root, "srcds", "cfg", "server.cfg")));
    }

    [TestMethod]
    public void Writing_will_not_create_a_file_that_is_not_there()
    {
        // Editing config is the job; creating arbitrary files on the box is not.
        Assert.ThrowsExactly<FileNotFoundException>(() => _files.Write("srcds", "cfg/new.cfg", "x"));
    }
}
