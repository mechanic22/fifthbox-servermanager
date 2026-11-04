using FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Tests;

[TestClass]
public class WorkloadLogsViewTests
{
    private static List<WorkloadLogLine> Lines(int count, string text = "x", bool previous = false) =>
        Enumerable.Range(0, count).Select(_ => new WorkloadLogLine { Text = text, Previous = previous }).ToList();

    [TestMethod]
    public void It_starts_on_the_last_500()
    {
        var view = new WorkloadLogsView();

        Assert.AreEqual(500, view.Tail);
        Assert.IsFalse(view.Loaded);
    }

    [TestMethod]
    public void Previous_runs_go_above_the_current_one()
    {
        var view = new WorkloadLogsView();

        view.SetCurrent(Lines(1, "now"), 100);
        view.SetPrevious(Lines(1, "before", previous: true));

        CollectionAssert.AreEqual(new[] { "before", "now" }, view.Lines.Select(l => l.Text).ToArray());
    }

    [TestMethod]
    public void A_full_page_means_there_may_be_more()
    {
        var view = new WorkloadLogsView();

        view.SetCurrent(Lines(500), 500);

        Assert.IsTrue(view.CanLoadEarlier);
        Assert.AreEqual(2_000, view.NextTail);
    }

    [TestMethod]
    public void Fewer_lines_than_asked_for_is_everything()
    {
        var view = new WorkloadLogsView();

        view.SetCurrent(Lines(40), 500);

        Assert.IsFalse(view.CanLoadEarlier);
    }

    [TestMethod]
    public void It_stops_offering_more_at_the_last_step()
    {
        var view = new WorkloadLogsView();

        view.SetCurrent(Lines(2_000), 2_000);

        Assert.IsFalse(view.CanLoadEarlier);
    }

    [TestMethod]
    public void Live_lines_add_to_the_current_run_and_make_a_new_list()
    {
        var view = new WorkloadLogsView();
        view.SetCurrent(Lines(1), 100);
        var before = view.Lines;

        view.Append(Lines(2));

        Assert.HasCount(3, view.Lines);
        Assert.AreNotSame(before, view.Lines);
    }

    [TestMethod]
    public void Live_lines_cant_grow_forever()
    {
        var view = new WorkloadLogsView();
        view.SetCurrent(Lines(2_000), 2_000);

        view.Append(Lines(1_000));

        Assert.HasCount(2_500, view.Lines);
    }
}
