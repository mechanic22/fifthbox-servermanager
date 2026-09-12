using System.IO.Compression;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent;

/// Fetches a workload's files into the directory the agent owns for it. One implementation per
/// SourceKind; the manager picks by kind and never knows how any of them work.
public interface ISourceProvider
{
    SourceKind Kind { get; }

    /// Returns the version marker to record on success. Progress goes to <paramref name="log"/> so it
    /// reaches the operator's Logs tab while it runs.
    Task<string> AcquireAsync(AgentWorkloadSpec spec, string installRoot, Action<string> log, CancellationToken ct);
}

public sealed class ZipSourceProvider(IHttpClientFactory httpFactory) : ISourceProvider
{
    public SourceKind Kind => SourceKind.Zip;

    public async Task<string> AcquireAsync(AgentWorkloadSpec spec, string installRoot, Action<string> log, CancellationToken ct)
    {
        var url = spec.Source?.Url ?? throw new InvalidOperationException("No source URL configured.");
        var temp = Path.Combine(Path.GetTempPath(), $"fbsm-{Guid.NewGuid():n}.zip");

        try
        {
            log($"Downloading {url}");

            var http = httpFactory.CreateClient();
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();

                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var file = File.Create(temp);
                await source.CopyToAsync(file, ct);
            }

            var size = new FileInfo(temp).Length;
            log($"Downloaded {size / (1024 * 1024)} MB; extracting into {installRoot}");

            // ExtractToDirectory refuses entries that resolve outside the destination, which is the whole
            // zip-slip defence here — don't replace this with a hand-rolled loop without carrying it over.
            ZipFile.ExtractToDirectory(temp, installRoot, overwriteFiles: true);

            log("Extracted.");
            return $"{Path.GetFileName(new Uri(url).AbsolutePath)} ({size} bytes)";
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // A leftover temp file is the OS's problem, not a reason to fail an acquire that worked.
        }
    }
}
