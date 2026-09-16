using System.Text;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Agent;

/// every operator path goes through InstallPaths or this is open access to the machine
public sealed class WorkloadFiles(IOptions<AgentOptions> options)
{
    /// big enough for any game config, small enough for a SignalR frame
    public const int MaxBytes = 512 * 1024;

    private readonly string _root = options.Value.ResolvedRootPath;

    public IReadOnlyList<WorkloadFileEntry> List(string workloadName, string? relativePath)
    {
        var directory = Resolve(workloadName, relativePath, allowRoot: true);

        if (!Directory.Exists(directory))
        {
            return [];
        }

        var installRoot = InstallRoot(workloadName);

        return [.. new DirectoryInfo(directory).EnumerateFileSystemInfos()
            .OrderByDescending(e => e is DirectoryInfo)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new WorkloadFileEntry
            {
                Name = e.Name,
                Path = RelativePath(installRoot, e.FullName),
                IsDirectory = e is DirectoryInfo,
                Size = e is FileInfo file ? file.Length : 0,
                ModifiedAt = e.LastWriteTimeUtc,
            })];
    }

    public WorkloadFileContent Read(string workloadName, string relativePath)
    {
        var path = Resolve(workloadName, relativePath, allowRoot: false);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"'{relativePath}' isn't there.");
        }

        var length = new FileInfo(path).Length;
        if (length > MaxBytes)
        {
            throw new InvalidOperationException($"'{relativePath}' is {length / 1024} KB — too big to edit here.");
        }

        var bytes = File.ReadAllBytes(path);

        // NUL byte means binary. configs come with any extension so no allowlist
        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            throw new InvalidOperationException($"'{relativePath}' looks like a binary file.");
        }

        return new WorkloadFileContent
        {
            Path = RelativePath(InstallRoot(workloadName), path),
            Text = new UTF8Encoding(false).GetString(bytes),
        };
    }

    public void Write(string workloadName, string relativePath, string text)
    {
        var path = Resolve(workloadName, relativePath, allowRoot: false);

        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            throw new InvalidOperationException("That's too big to write here.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"'{relativePath}' isn't there. This edits existing files; it doesn't create them.");
        }

        // no BOM, it shows up as junk on the config's first line
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private string InstallRoot(string workloadName) => InstallPaths.TryRootFor(_root, workloadName, out var root)
        ? root
        : throw new InvalidOperationException($"'{workloadName}' can't be used as a directory name.");

    private string Resolve(string workloadName, string? relativePath, bool allowRoot)
    {
        var installRoot = InstallRoot(workloadName);

        if (string.IsNullOrWhiteSpace(relativePath) || relativePath is "." or "/")
        {
            return allowRoot ? installRoot : throw new InvalidOperationException("No file named.");
        }

        return InstallPaths.TryResolveWithin(installRoot, relativePath.Replace('/', Path.DirectorySeparatorChar), out var full)
            ? full
            : throw new InvalidOperationException($"'{relativePath}' is outside this workload's directory.");
    }

    private static string RelativePath(string installRoot, string fullPath) =>
        Path.GetRelativePath(installRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
}
