using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Components;

public static class UsageChartRange
{
    public static TimeSpan Window(StatsHistoryRange range) =>
        range == StatsHistoryRange.Day ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(30);

    /// samples are 10s apart and thinned ones a minute apart, this lets one or two go missing
    public static long MaxGapSeconds(StatsHistoryRange range) => range == StatsHistoryRange.Day ? 180 : 45;

    /// pushes come every 10s, the chart doesn't need to refetch that often
    public static TimeSpan RefreshEvery(StatsHistoryRange range) =>
        range == StatsHistoryRange.Day ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(30);
}
