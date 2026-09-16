namespace FifthBox.ServerManager.Agent;

/// every containment check on operator paths goes through here
public static class InstallPaths
{
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// both slashes on every platform, a linux manager can send "..\\escape" to a windows agent
    private static readonly char[] NameSeparators = ['/', '\\'];

    /// case-sensitive off windows, too strict is the safe way to be wrong
    private static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// Host slugifies names already but check anyway, this is where a hostile one lands
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

    /// doesn't follow symlinks, a link planted inside can still point out
    public static bool TryResolveWithin(string installRoot, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var root = Path.GetFullPath(installRoot);

        // Path.Combine ignores root if this is rooted, so it'd sail past the check below
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

    /// absolute paths pass as is, bare names go to PATH (java, dotnet), anything else must stay in the install root
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
