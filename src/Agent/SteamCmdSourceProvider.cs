using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Agent;

/// steamcmd itself gets bootstrapped once into a shared folder under the agent root
public sealed partial class SteamCmdSourceProvider(IOptions<AgentOptions> options, IHttpClientFactory httpFactory) : ISourceProvider
{
    public SourceKind Kind => SourceKind.SteamCmd;

    private readonly string _toolsRoot = Path.Combine(Path.GetFullPath(options.Value.ResolvedRootPath), "_tools", "steamcmd");

    public async Task<string> AcquireAsync(AgentWorkloadSpec spec, string installRoot, Action<string> log, CancellationToken ct)
    {
        var source = spec.Source ?? throw new InvalidOperationException("No source configured.");
        var appId = source.SteamAppId ?? throw new InvalidOperationException("No Steam app id configured.");

        var steamCmd = await EnsureSteamCmdAsync(log, ct);

        var arguments = new List<string> { "+force_install_dir", installRoot };

        if (string.IsNullOrWhiteSpace(source.SteamUsername))
        {
            arguments.AddRange(["+login", "anonymous"]);
        }
        else
        {
            arguments.AddRange(["+login", source.SteamUsername, source.SteamPassword ?? string.Empty]);
        }

        arguments.AddRange(["+app_update", appId.ToString()]);

        if (!string.IsNullOrWhiteSpace(source.SteamBranch))
        {
            arguments.AddRange(["-beta", source.SteamBranch]);
        }

        arguments.AddRange(["validate", "+quit"]);

        log($"Running steamcmd for app {appId}{(source.SteamBranch is { Length: > 0 } b ? $" on branch {b}" : "")}");

        var buildId = await RunAsync(steamCmd, arguments, log, ct);
        return buildId is null ? $"app {appId}" : $"app {appId} build {buildId}";
    }

    /// returns the last build id steamcmd printed, that's what ended up on disk
    private static async Task<string?> RunAsync(string steamCmd, List<string> arguments, Action<string> log, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = steamCmd,
            WorkingDirectory = Path.GetDirectoryName(steamCmd),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start steamcmd.");

        string? buildId = null;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            // a failed login echoes the account name, so don't log anything beyond what steamcmd prints
            log(e.Data);

            if (BuildId().Match(e.Data) is { Success: true } match)
            {
                buildId = match.Groups[1].Value;
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                log(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);

        // 7 means already up to date on some versions
        if (process.ExitCode is not (0 or 7))
        {
            throw new InvalidOperationException($"steamcmd exited with {process.ExitCode}.");
        }

        return buildId;
    }

    private async Task<string> EnsureSteamCmdAsync(Action<string> log, CancellationToken ct)
    {
        var executable = Path.Combine(_toolsRoot, OperatingSystem.IsWindows() ? "steamcmd.exe" : "steamcmd.sh");

        if (File.Exists(executable))
        {
            return executable;
        }

        Directory.CreateDirectory(_toolsRoot);
        log("Bootstrapping steamcmd");

        var url = OperatingSystem.IsWindows()
            ? "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"
            : "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz";

        var archive = Path.Combine(_toolsRoot, OperatingSystem.IsWindows() ? "steamcmd.zip" : "steamcmd.tar.gz");

        var http = httpFactory.CreateClient();
        using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(archive);
            await stream.CopyToAsync(file, ct);
        }

        if (OperatingSystem.IsWindows())
        {
            ZipFile.ExtractToDirectory(archive, _toolsRoot, overwriteFiles: true);
        }
        else
        {
            await ExtractTarGzAsync(archive, _toolsRoot, ct);
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        File.Delete(archive);
        log("steamcmd ready");
        return executable;
    }

    private static async Task ExtractTarGzAsync(string archive, string destination, CancellationToken ct)
    {
        await using var file = File.OpenRead(archive);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        await System.Formats.Tar.TarFile.ExtractToDirectoryAsync(gzip, destination, overwriteFiles: true, ct);
    }

    [GeneratedRegex(@"build\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex BuildId();
}
