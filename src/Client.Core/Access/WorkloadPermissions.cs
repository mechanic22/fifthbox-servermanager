using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Core;

/// UI hints only, the server checks again on every call
public static class WorkloadPermissions
{
    public static bool CanView(WorkloadResponse w) => w.Access >= AccessLevel.View;

    public static bool CanOperate(WorkloadResponse w) => w.Access >= AccessLevel.Operate;

    public static bool CanConfigure(WorkloadResponse w) => w.Access >= AccessLevel.Configure;

    /// the server decides this (App's DeployPermission), not a level check
    public static bool CanDeploy(WorkloadResponse w) => w.CanDeploy;
}
