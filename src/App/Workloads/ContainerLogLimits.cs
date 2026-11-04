namespace FifthBox.ServerManager.App.Workloads;

/// docker's default json-file has no cap, a chatty container fills the disk
public static class ContainerLogLimits
{
    public const string Driver = "json-file";
    public const string MaxSize = "50m";
    public const string MaxFile = "3";
}
