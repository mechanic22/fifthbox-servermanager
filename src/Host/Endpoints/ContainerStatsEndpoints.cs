using FifthBox.ServerManager.App.ContainerStats;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Host.Endpoints;

public static class ContainerStatsEndpoints
{
    public static IEndpointRouteBuilder MapContainerStatsEndpoints(this IEndpointRouteBuilder app)
    {
        // fbsm-stats has no account, the signature on each report is the credential
        app.MapPost("/api/internal/container-stats", async (HttpRequest request, IContainerStatsIngest ingest, CancellationToken ct) =>
            {
                await ingest.IngestAsync(
                    await ReadCappedAsync(request, ct),
                    request.Headers[ContainerStatsHeaders.Timestamp],
                    request.Headers[ContainerStatsHeaders.Signature],
                    ct);
                return TypedResults.NoContent();
            })
            .AllowAnonymous()
            .DisableAntiforgery();

        return app;
    }

    private static async Task<byte[]> ReadCappedAsync(HttpRequest request, CancellationToken ct)
    {
        using var body = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(buffer, ct)) > 0)
        {
            body.Write(buffer, 0, read);
            if (body.Length > ContainerStatsIngest.MaxBodyBytes)
            {
                throw new ValidationException("report", "The report is too large.");
            }
        }

        return body.ToArray();
    }
}
