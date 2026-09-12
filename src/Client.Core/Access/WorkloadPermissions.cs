using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Core;

/// What the UI should offer for one workload, so buttons don't appear that would come back 403. The
/// server decides the same thing again on every call.
public static class WorkloadPermissions
{
    public static bool CanView(WorkloadResponse w) => w.Access >= AccessLevel.View;

    public static bool CanOperate(WorkloadResponse w) => w.Access >= AccessLevel.Operate;

    public static bool CanConfigure(WorkloadResponse w) => w.Access >= AccessLevel.Configure;

    /// Not a level comparison — App's DeployPermission decides it and sends the answer on the response,
    /// so the rule lives in exactly one place.
    public static bool CanDeploy(WorkloadResponse w) => w.CanDeploy;
}
