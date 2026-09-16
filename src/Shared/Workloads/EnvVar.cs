namespace FifthBox.ServerManager.Shared.Workloads;

/// Secret means Value is stored encrypted and comes back blank
public record EnvVar(string Key, string Value, bool Secret = false);
