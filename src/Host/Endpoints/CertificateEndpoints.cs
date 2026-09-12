using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.Shared.Auth;

namespace FifthBox.ServerManager.Host.Endpoints;

public static class CertificateEndpoints
{
    public static IEndpointRouteBuilder MapCertificateEndpoints(this IEndpointRouteBuilder app)
    {
        // Let's Encrypt fetches this from the internet over plain port 80, unauthenticated — the token
        // is the secret. An unknown token gets a bare 404 rather than ProblemDetails: the validation
        // server only reads the status, and there's no reason to echo the token back.
        app.MapGet("/.well-known/acme-challenge/{token}", (string token, IAcmeChallengeStore challenges) =>
                challenges.Resolve(token) is { } keyAuthorization
                    ? Results.Text(keyAuthorization, "text/plain")
                    : Results.NotFound())
            .AllowAnonymous()
            .DisableAntiforgery();

        var group = app.MapGroup("/api/tls").RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapGet("/", async (ICertificateService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.OverviewAsync(ct)));

        group.MapPost("/{hostname}", async (string hostname, ICertificateService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.EnableAsync(hostname, ct)));

        group.MapPost("/{hostname}/retry", async (string hostname, ICertificateService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.RetryAsync(hostname, ct)));

        group.MapDelete("/{hostname}", async (string hostname, ICertificateService svc, CancellationToken ct) =>
        {
            await svc.DisableAsync(hostname, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
