using FifthBox.ServerManager.App.Cleanup;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class ImageRetentionTests
{
    private static LocalImage Image(string id, string tag, int day, long size = 100) =>
        new(id, tag.Length == 0 ? [] : [tag], [], size, new DateTimeOffset(2026, 10, day, 0, 0, 0, TimeSpan.Zero));

    private static string[] Removed(IReadOnlyList<LocalImage> images, params string[] inUse) =>
        [.. ImageRetention.SelectForRemoval(images, inUse.ToHashSet()).Select(i => i.Id).Order()];

    [TestMethod]
    public void Keeps_what_runs_and_the_previous_tag_of_it()
    {
        var images = new[]
        {
            Image("a7", "docker.5thbox.com/fbsm/servermanager:0.2.7", 1),
            Image("a8", "docker.5thbox.com/fbsm/servermanager:0.2.8", 2),
            Image("a9", "docker.5thbox.com/fbsm/servermanager:0.2.9", 3),
        };

        CollectionAssert.AreEqual(new[] { "a7" }, Removed(images, "a9"));
    }

    [TestMethod]
    public void Images_nothing_uses_go()
    {
        var images = new[] { Image("x", "alpine:3.20", 1), Image("site", "sites/stjohn:1.0", 2) };

        CollectionAssert.AreEqual(new[] { "x" }, Removed(images, "site"));
    }

    [TestMethod]
    public void A_registry_port_isnt_mistaken_for_a_tag()
    {
        var images = new[]
        {
            Image("old", "registry:5000/app:1", 1),
            Image("mid", "registry:5000/app:2", 2),
            Image("new", "registry:5000/app:3", 3),
        };

        CollectionAssert.AreEqual(new[] { "old" }, Removed(images, "new"));
    }

    [TestMethod]
    public void Untagged_leftovers_of_a_running_repo_count_by_digest()
    {
        var running = Image("run", "sites/trf:2", 5);
        var dangling = new LocalImage("dang", [], ["sites/trf@sha256:abc"], 50, new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero));
        var older = Image("older", "sites/trf:1", 1);

        // the dangling one is the newer of the two leftovers, so it's the rollback copy that stays
        CollectionAssert.AreEqual(new[] { "older" }, Removed([running, dangling, older], "run"));
    }

    [TestMethod]
    public void Names_prefer_the_tag_then_digest_then_short_id()
    {
        Assert.AreEqual("app:1", ImageRetention.NameOf(Image("sha256:0123456789abcdef", "app:1", 1)));
        Assert.AreEqual("app@sha256:abc", ImageRetention.NameOf(new LocalImage("i", [], ["app@sha256:abc"], 1, default)));
        Assert.AreEqual("0123456789ab", ImageRetention.NameOf(new LocalImage("sha256:0123456789abcdef", ["<none>:<none>"], [], 1, default)));
    }
}

[TestClass]
public class ImageCleanupServiceTests
{
    private readonly Mock<IDockerImages> _docker = new();
    private readonly ImageCleanupService _service;

    public ImageCleanupServiceTests()
    {
        var day = (int d) => new DateTimeOffset(2026, 10, d, 0, 0, 0, TimeSpan.Zero);
        _docker.Setup(d => d.ListImagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new LocalImage("run", ["app:3"], [], 300, day(3)),
            new LocalImage("prev", ["app:2"], [], 200, day(2)),
            new LocalImage("old", ["app:1"], [], 100, day(1)),
            new LocalImage("stray", ["busybox:1"], [], 50, day(1)),
        ]);
        _docker.Setup(d => d.ImageIdsInUseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<string> { "run" });
        _docker.Setup(d => d.CountVolumesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(4);
        _service = new ImageCleanupService(_docker.Object);
    }

    [TestMethod]
    public async Task Preview_lists_what_would_go_biggest_first()
    {
        var preview = await _service.PreviewAsync();

        Assert.AreEqual(4, preview.ImageCount);
        Assert.AreEqual(650, preview.ImageBytes);
        Assert.AreEqual(4, preview.VolumeCount);
        CollectionAssert.AreEqual(new[] { "app:1", "busybox:1" }, preview.Removable.Select(r => r.Name).ToArray());
        Assert.AreEqual(150, preview.ReclaimableBytes);
    }

    [TestMethod]
    public async Task Remove_counts_what_docker_refused()
    {
        _docker.Setup(d => d.TryRemoveImageAsync("old", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _docker.Setup(d => d.TryRemoveImageAsync("stray", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.RemoveUnusedAsync();

        Assert.AreEqual(1, result.Removed);
        Assert.AreEqual(100, result.FreedBytes);
        Assert.AreEqual(1, result.Skipped);
        _docker.Verify(d => d.TryRemoveImageAsync("run", It.IsAny<CancellationToken>()), Times.Never);
        _docker.Verify(d => d.TryRemoveImageAsync("prev", It.IsAny<CancellationToken>()), Times.Never);
    }
}
