using System.Security.Cryptography;
using System.Text;
using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.Agent;

/// The checks that decide whether a running process is the one we think it is. Pure so they can be
/// tested without spawning anything.
public static class ProcessAdoption
{
    /// A PID alone proves nothing — the OS reuses them, and after a reboot the PID we recorded is very
    /// likely someone else's process. The start time is what makes the identity stick.
    public static bool IsSameProcess(DateTimeOffset recordedStart, DateTimeOffset actualStart)
        => (recordedStart - actualStart).Duration() <= TimeSpan.FromSeconds(1);

    /// Whether a running workload is already on the config we're being asked to deploy. Record equality
    /// won't do it — the list members compare by reference.
    public static bool SameConfig(AgentWorkloadSpec? a, AgentWorkloadSpec? b)
        => a is not null && b is not null && HashOf(a) == HashOf(b);

    /// The comparable form of a spec. Hashed rather than kept as the signature itself because it is
    /// written to the state file, and the signature contains environment values verbatim.
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
