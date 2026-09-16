using System.Security.Cryptography;
using System.Text;
using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.Agent;

public static class ProcessAdoption
{
    /// pids get reused, esp after a reboot. start time is what proves it's ours
    public static bool IsSameProcess(DateTimeOffset recordedStart, DateTimeOffset actualStart)
        => (recordedStart - actualStart).Duration() <= TimeSpan.FromSeconds(1);

    /// not record equality, the list members compare by reference
    public static bool SameConfig(AgentWorkloadSpec? a, AgentWorkloadSpec? b)
        => a is not null && b is not null && HashOf(a) == HashOf(b);

    /// hashed because it lands in the state file and the signature has env values in plain text
    public static string HashOf(AgentWorkloadSpec spec)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(SignatureOf(spec))));

    private const string Unit = "\u001f";

    private static string SignatureOf(AgentWorkloadSpec s) => string.Join('|',
        s.Command,
        string.Join(Unit, s.Args),
        s.WorkingDirectory ?? string.Empty,
        string.Join(Unit, s.Env.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key}={e.Value}")),
        s.RestartPolicy,
        s.StopGraceSeconds,
        s.StopCommand ?? string.Empty,
        s.ManagedDirectory,
        s.Source?.Kind,
        s.Source?.Url ?? string.Empty,
        s.Source?.SteamAppId,
        s.Source?.SteamBranch ?? string.Empty,
        s.Source?.SteamUsername ?? string.Empty);
}
