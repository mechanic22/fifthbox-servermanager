using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Registries;

namespace FifthBox.ServerManager.Host.Endpoints;

/// Private Docker registry credentials — admin-only CRUD. Passwords are write-only; the API never
/// returns them. Translate + delegate only.
public static class RegistryEndpoints
{
    public static IEndpointRouteBuilder MapRegistryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/registries").RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapGet("/", async (IRegistryService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(ct)));

        group.MapPost("/", async (CreateRegistryRequest request, IRegistryService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.CreateAsync(request, ct)));

        group.MapPut("/{id}", async (string id, UpdateRegistryRequest request, IRegistryService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.UpdateAsync(id, request, ct)));

        group.MapDelete("/{id}", async (string id, IRegistryService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
