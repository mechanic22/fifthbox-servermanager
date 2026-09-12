using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Platform;

namespace FifthBox.ServerManager.Host.Endpoints;

/// ServerManager's own infrastructure: platform services (nginx, etc.) list + restart, and platform-wide
/// settings. Admin-only. Translate + delegate only.
public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system").RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapGet("/services", async (IPlatformServices svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(ct)));

        group.MapPost("/services/{name}/restart", async (string name, IPlatformServices svc, CancellationToken ct) =>
        {
            await svc.RestartAsync(name, ct);
            return TypedResults.NoContent();
        });

        group.MapGet("/deployment", async (IHostDeploymentService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetAsync(ct)));

        group.MapGet("/settings", async (IPlatformSettingsService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetAsync(ct)));

        group.MapPut("/settings", async (UpdatePlatformSettingsRequest request, IPlatformSettingsService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.UpdateAsync(request, ct)));

        group.MapPost("/encryption/rotate", async (RotateEncryptionKeyRequest request, ISecretCustodyService svc, CancellationToken ct) =>
            TypedResults.Ok(new RotateEncryptionKeyResponse { SecretsRewritten = await svc.RotateAsync(request.NewKey, ct) }));

        group.MapGet("/backups", (IBackupService svc) => TypedResults.Ok(svc.List()));

        group.MapPost("/backups", async (IBackupService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.CreateAsync(ct)));

        group.MapGet("/backups/{name}", (string name, IBackupService svc) =>
            TypedResults.Stream(svc.OpenRead(name), "application/octet-stream", name));

        group.MapDelete("/backups/{name}", (string name, IBackupService svc) =>
        {
            svc.Delete(name);
            return TypedResults.NoContent();
        });

        group.MapGet("/jobs", (IScheduledJobs jobs) => TypedResults.Ok(jobs.List()));

        group.MapPost("/jobs/{name}/run", async (string name, IScheduledJobs jobs, CancellationToken ct) =>
            TypedResults.Ok(await jobs.RunNowAsync(name, ct)));

        return app;
    }
}
