using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.Shared.Exceptions;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class BackupRetentionTests
{
    private static BackupFile At(string name, int day) => new(name, 100, new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void Keeps_the_newest_and_deletes_the_rest()
    {
        var stale = BackupRetention.SelectForDeletion([At("a.db", 1), At("c.db", 3), At("b.db", 2)], keep: 2);

        CollectionAssert.AreEqual(new[] { "a.db" }, stale.Select(f => f.Name).ToArray());
    }

    [TestMethod]
    public void Deletes_nothing_when_under_the_limit()
    {
        Assert.IsEmpty(BackupRetention.SelectForDeletion([At("a.db", 1), At("b.db", 2)], keep: 5));
    }

    [TestMethod]
    public void Always_keeps_at_least_one_however_it_is_configured()
    {
        var stale = BackupRetention.SelectForDeletion([At("a.db", 1), At("b.db", 2)], keep: 0);

        CollectionAssert.AreEqual(new[] { "a.db" }, stale.Select(f => f.Name).ToArray());
    }
}

[TestClass]
public class BackupServiceTests
{
    private const string Dir = "backups";

    private static (BackupService svc, Mock<IBackupStore> store) Build(params BackupFile[] existing)
    {
        var store = new Mock<IBackupStore>();
        store.Setup(s => s.List(Dir)).Returns(existing.ToList());
        store.Setup(s => s.WriteAsync(Dir, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string name, CancellationToken _) => new BackupFile(name, 10, DateTimeOffset.UnixEpoch));

        var options = Options.Create(new BackupOptions { Directory = Dir, Keep = 2 });
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
        return (new BackupService(store.Object, options, clock), store);
    }

    [TestMethod]
    public async Task Create_names_the_file_from_the_clock()
    {
        var (svc, _) = Build();

        var created = await svc.CreateAsync();

        Assert.AreEqual("fbsm-20260304-050607.db", created.Name);
    }

    [TestMethod]
    public async Task CreateAndPrune_deletes_only_what_falls_outside_retention()
    {
        var (svc, store) = Build(
            new BackupFile("old.db", 1, DateTimeOffset.UnixEpoch),
            new BackupFile("newer.db", 1, DateTimeOffset.UnixEpoch.AddDays(1)),
            new BackupFile("newest.db", 1, DateTimeOffset.UnixEpoch.AddDays(2)));

        await svc.CreateAndPruneAsync();

        store.Verify(s => s.Delete(Dir, "old.db"), Times.Once);
        store.Verify(s => s.Delete(Dir, It.IsAny<string>()), Times.Once);
    }

    [DataRow("../../etc/passwd")]
    [DataRow("sub/dir.db")]
    [DataRow("notabackup.txt")]
    [DataRow("")]
    [TestMethod]
    public void Reads_reject_anything_that_is_not_a_bare_backup_file_name(string name)
    {
        var (svc, _) = Build();

        Assert.ThrowsExactly<ValidationException>(() => svc.OpenRead(name));
    }

    [TestMethod]
    public void Reading_a_backup_that_is_not_there_is_a_not_found()
    {
        var (svc, _) = Build();

        Assert.ThrowsExactly<NotFoundException>(() => svc.OpenRead("fbsm-20260101-000000.db"));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
