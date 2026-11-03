namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

public enum WorkloadAction
{
    None,
    Deploy,
    Start,
}

public static class WorkloadPrimaryAction
{
    /// <param name="gone">should be running, but nothing's deployed</param>
    public static WorkloadAction For(
        bool canDeploy,
        bool canOperate,
        bool deploying,
        bool neverDeployed,
        bool stalled,
        bool drift,
        bool pendingChanges,
        bool gone,
        bool stopped)
    {
        if (deploying)
        {
            return WorkloadAction.None;
        }

        if (canDeploy && (neverDeployed || stalled || drift || pendingChanges || gone))
        {
            return WorkloadAction.Deploy;
        }

        return stopped && canOperate && !neverDeployed ? WorkloadAction.Start : WorkloadAction.None;
    }
}
