namespace FifthBox.ServerManager.Agent;

/// Where a managed workload lives on disk, and what is allowed to be reached from there. Pure, because
/// every containment decision the agent makes about operator-supplied paths goes through here.
public static class InstallPaths
{
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// Both slashes, whatever this platform thinks. A backslash is an ordinary filename character on
    /// Linux, so a Linux-hosted manager would happily pass "..\\escape" to a Windows agent, where it
    /// traverses. Names are slugified upstream and never contain either.
    private static readonly char[] NameSeparators = ['/', '\\'];

    /// Windows paths are case-insensitive; assuming they aren't elsewhere can only make containment
    /// stricter, which is the safe direction to be wrong in.
    private static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// The directory the agent owns for one workload. The name has to be a single ordinary path segment:
    /// the Host slugifies it, but this is the boundary where a hostile name would land, so check anyway.
    public static bool TryRootFor(string agentRoot, string workloadName, out string installRoot)
    {
        installRoot = string.Empty;

        if (string.IsNullOrWhiteSpace(workloadName)
            || workloadName is "." or ".."
            || workloadName.IndexOfAny(NameSeparators) >= 0
            || workloadName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        installRoot = Path.Combine(Path.GetFullPath(agentRoot), workloadName);
        return true;
    }

    /// Resolve a path that is meant to sit inside the install root, refusing anything that climbs out.
    /// Does not resolve symlinks — a link planted inside the root can still point elsewhere, so this is
    /// a guard against traversal, not against someone who can already write into the directory.
    public static bool TryResolveWithin(string installRoot, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var root = Path.GetFullPath(installRoot);

        // Path.Combine drops the first argument entirely when the second is rooted, so an absolute path
        // would sail through the containment check below if it weren't rejected here.
        if (Path.IsPathRooted(relativePath))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!IsWithin(root, candidate))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// What to hand Process.Start as the file name for a managed workload. Three cases, in order:
    /// an absolute path is the operator's own choice; a bare name with no separator is left for the OS
    /// to find on PATH (this is what makes `java` or `dotnet` work); anything else is relative to the
    /// install root and has to stay inside it.
    public static bool TryResolveCommand(string installRoot, string command, out string resolved)
    {
        resolved = string.Empty;

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (Path.IsPathRooted(command) || command.IndexOfAny(Separators) < 0)
        {
            resolved = command;
            return true;
        }

        return TryResolveWithin(installRoot, command, out resolved);
    }

    private static bool IsWithin(string root, string candidate)
    {
        var trimmedRoot = root.TrimEnd(Separators);

        return candidate.StartsWith(trimmedRoot + Path.DirectorySeparatorChar, Comparison)
               || candidate.TrimEnd(Separators).Equals(trimmedRoot, Comparison);
    }
}
