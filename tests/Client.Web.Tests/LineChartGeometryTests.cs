using FifthBox.ServerManager.Client.Web.Components;

namespace FifthBox.ServerManager.Client.Web.Tests;

[TestClass]
public class LineChartGeometryTests
{
    [TestMethod]
    [DataRow(0.0, 1.0)]
    [DataRow(3.0, 5.0)]
    [DataRow(7.0, 10.0)]
    [DataRow(12.0, 20.0)]
    [DataRow(100.0, 100.0)]
    [DataRow(101.0, 200.0)]
    public void The_axis_top_is_a_round_number(double value, double expected) =>
        Assert.AreEqual(expected, LineChartGeometry.NiceMax(value), 0.0001);

    [TestMethod]
    public void A_big_gap_breaks_the_line()
    {
        var segments = LineChartGeometry.Segments([new(0, 1), new(10, 1), new(500, 1), new(510, 1)], maxGapSeconds: 60);

        Assert.HasCount(2, segments);
        Assert.HasCount(2, segments[1]);
    }

    [TestMethod]
    public void No_points_means_no_segments() =>
        Assert.IsEmpty(LineChartGeometry.Segments([], 60));

    [TestMethod]
    public void Time_runs_left_to_right_across_the_window()
    {
        Assert.AreEqual(0, LineChartGeometry.X(100, 100, 200));
        Assert.AreEqual(LineChartGeometry.Width, LineChartGeometry.X(200, 100, 200));
    }

    [TestMethod]
    public void A_value_over_the_limit_stays_on_the_chart()
    {
        Assert.AreEqual(0, LineChartGeometry.Y(500, 100));
        Assert.AreEqual(LineChartGeometry.Height, LineChartGeometry.Y(0, 100));
    }

    [TestMethod]
    public void Paths_use_invariant_numbers() =>
        Assert.AreEqual("M0 60 L600 0", LineChartGeometry.LinePath([new(0, 50), new(100, 100)], 0, 100, 100));

    [TestMethod]
    public void The_area_closes_down_to_the_baseline() =>
        Assert.EndsWith("L600 120 L0 120 Z", LineChartGeometry.AreaPath([new(0, 50), new(100, 100)], 0, 100, 100));

    [TestMethod]
    public void The_crosshair_snaps_to_the_nearest_point()
    {
        Assert.AreEqual(1, LineChartGeometry.NearestIndex([new(0, 1), new(100, 1), new(200, 1)], 120));
        Assert.AreEqual(-1, LineChartGeometry.NearestIndex([], 120));
    }
}
