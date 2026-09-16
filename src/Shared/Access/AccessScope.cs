namespace FifthBox.ServerManager.Shared.Access;

/// group grants reach every workload under them, however deep
public enum AccessScope
{
    Workload = 0,
    Group = 1,
}
