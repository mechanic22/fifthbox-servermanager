using System.Globalization;
using System.Text;

namespace FifthBox.ServerManager.Client.Web.Components;

/// <param name="At">unix seconds</param>
public readonly record struct ChartPoint(long At, double Value);

/// TODO: promote to FifthBox.MaterialComponents.Blazor with LineChart
public static class LineChartGeometry
{
    public const int Width = 600;
    public const int Height = 120;

    /// 1, 2 or 5 times a power of ten, so the top of the axis reads as a round number
    public static double NiceMax(double value)
    {
        if (value <= 0)
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var step = value / magnitude;
        var nice = step <= 1 ? 1 : step <= 2 ? 2 : step <= 5 ? 5 : 10;
        return nice * magnitude;
    }

    public static double X(long at, long from, long to) =>
        to <= from ? 0 : Math.Clamp((at - from) / (double)(to - from), 0, 1) * Width;

    /// up is smaller y in svg. a value over the top stays on the chart
    public static double Y(double value, double max) =>
        Height - (Math.Clamp(value / max, 0, 1) * Height);

    /// a gap means nothing was reporting, drawing across it would make up data
    public static List<List<ChartPoint>> Segments(IReadOnlyList<ChartPoint> points, long maxGapSeconds)
    {
        var segments = new List<List<ChartPoint>>();
        foreach (var point in points)
        {
            if (segments.Count == 0 || point.At - segments[^1][^1].At > maxGapSeconds)
            {
                segments.Add([]);
            }

            segments[^1].Add(point);
        }

        return segments;
    }

    public static string LinePath(IReadOnlyList<ChartPoint> segment, long from, long to, double max)
    {
        var path = new StringBuilder();
        for (var i = 0; i < segment.Count; i++)
        {
            path.Append(i == 0 ? 'M' : 'L').Append(Number(X(segment[i].At, from, to))).Append(' ').Append(Number(Y(segment[i].Value, max))).Append(' ');
        }

        return path.ToString().TrimEnd();
    }

    public static string AreaPath(IReadOnlyList<ChartPoint> segment, long from, long to, double max) =>
        segment.Count == 0
            ? string.Empty
            : $"{LinePath(segment, from, to, max)} L{Number(X(segment[^1].At, from, to))} {Height} L{Number(X(segment[0].At, from, to))} {Height} Z";

    /// -1 with no points
    public static int NearestIndex(IReadOnlyList<ChartPoint> points, long at)
    {
        var best = -1;
        var bestDistance = long.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            var distance = Math.Abs(points[i].At - at);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
