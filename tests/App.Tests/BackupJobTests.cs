using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.Shared.Platform;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class BackupScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Due_when_nothing_has_been_backed_up_yet() =>
        Assert.IsTrue(BackupSchedule.IsDue(null, Now, TimeSpan.FromHours(24)));

    [TestMethod]
    public void Due_once_the_newest_backup_is_older_than_the_interval() =>
        Assert.IsTrue(BackupSchedule.IsDue(Now.AddHours(-25), Now, TimeSpan.FromHours(24)));

    [TestMethod]
    public void Not_due_while_the_newest_backup_is_inside_the_interval() =>
        Assert.IsFalse(BackupSchedule.IsDue(Now.AddHours(-3), Now, TimeSpan.FromHours(24)));

    [TestMethod]
    public void Not_due_when_a_backup_is_stamped_in_the_future() =>
        Assert.IsFalse(BackupSchedule.IsDue(Now.AddHours(1), Now, TimeSpan.FromHours(24)));
}

[TestClass]
public class BackupJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);

    private static (BackupJob job, Mock<IBackupService> backups) Build(BackupOptions options, params DateTimeOffset[] existing)
    {
        var backups = new Mock<IBackupService>();
        backups.Setup(b => b.List()).Returns(existing
            .OrderByDescending(t => t)
            .Select(t => new BackupFileResponse { Name = $"fbsm-{t:yyyyMMdd-HHmmss}.db", SizeBytes = 1, CreatedAt = t })
            .ToList());
        return (new BackupJob(backups.Object, Options.Create(options), new FakeTimeProvider(Now)), backups);
    }

    [TestMethod]
    public async Task Backs_up_when_the_newest_backup_predates_the_interval()
    {
        var (job, backups) = Build(new BackupOptions { IntervalHours = 24 }, Now.AddHours(-30));

        await job.RunAsync();

        backups.Verify(b => b.CreateAndPruneAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Backs_up_when_there_are_no_backups_at_all()
    {
        var (job, backups) = Build(new BackupOptions { IntervalHours = 24 });

        await job.RunAsync();

        backups.Verify(b => b.CreateAndPruneAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // The runner's last-run state dies with the process, so a restart re-runs every job immediately.
    [TestMethod]
    public async Task Skips_a_restart_that_lands_inside_the_interval()
    {
        var (job, backups) = Build(new BackupOptions { IntervalHours = 24 }, Now.AddHours(-2));

        await job.RunAsync();

        backups.Verify(b => b.CreateAndPruneAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Does_nothing_when_scheduled_backups_are_turned_off()
    {
        var (job, backups) = Build(new BackupOptions { IntervalHours = 24, Enabled = false });

        await job.RunAsync();

        backups.Verify(b => b.CreateAndPruneAsync(It.IsAny<CancellationToken>()), Times.Never);
        backups.Verify(b => b.List(), Times.Never);
    }

    [TestMethod]
    public void Interval_never_drops_below_an_hour_however_it_is_configured()
    {
        var (job, _) = Build(new BackupOptions { IntervalHours = 0 });

        Assert.AreEqual(TimeSpan.FromHours(1), job.Interval);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
