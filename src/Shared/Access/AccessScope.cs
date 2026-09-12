namespace FifthBox.ServerManager.Shared.Access;

/// What a grant is attached to. A group grant reaches every workload beneath it, however deep.
public enum AccessScope
{
    Workload = 0,
    Group = 1,
}
