using System.Net.Http.Headers;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Stats;

public interface IReportSender
{
    Task SendAsync(ContainerStatsReport report, CancellationToken ct);
}

public sealed class HostReportSender(string hostUrl, string key, TimeProvider clock) : IReportSender, IDisposable
{
    // no redirects, a signed report only ever goes to the Host
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        BaseAddress = new Uri(hostUrl),
        Timeout = TimeSpan.FromSeconds(5),
    };

    public async Task SendAsync(ContainerStatsReport report, CancellationToken ct)
    {
        var envelope = ReportEnvelope.Build(report, key, clock.GetUtcNow());

        using var content = new ByteArrayContent(envelope.Body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/container-stats") { Content = content };
        request.Headers.Add(ContainerStatsHeaders.Timestamp, envelope.Timestamp);
        request.Headers.Add(ContainerStatsHeaders.Signature, envelope.Signature);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
