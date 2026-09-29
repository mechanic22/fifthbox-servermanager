using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.Shared.Auth;

namespace FifthBox.ServerManager.Host.Endpoints;

public static class CertificateEndpoints
{
    public static IEndpointRouteBuilder MapCertificateEndpoints(this IEndpointRouteBuilder app)
    {
        // let's encrypt hits this anonymously over port 80, the token is the secret
        // bare 404 on a miss, no ProblemDetails echoing the token back
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

        group.MapPut("/{hostname}/www", async (string hostname, ICertificateService svc, CancellationToken ct) =>
        {
            await svc.SetWwwRedirectAsync(hostname, enabled: true, ct);
            return TypedResults.NoContent();
        });

        group.MapDelete("/{hostname}/www", async (string hostname, ICertificateService svc, CancellationToken ct) =>
        {
            await svc.SetWwwRedirectAsync(hostname, enabled: false, ct);
            return TypedResults.NoContent();
        });

        group.MapDelete("/{hostname}", async (string hostname, ICertificateService svc, CancellationToken ct) =>
        {
            await svc.DisableAsync(hostname, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
