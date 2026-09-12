using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.Identity;
using FifthBox.Identity.Abstractions;
using FifthBox.Identity.AspNetCore;
using FifthBox.Identity.Contracts;
using FifthBox.ServerManager.Storage.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FifthBox.ServerManager.Host.Endpoints;

/// <summary>
/// Auth endpoints. Each lambda just does HTTP and hands off to <see cref="IIdentityService"/>; the
/// service throws, the exception handler maps it. Browser flows sign a cookie (no tokens); native
/// flows return bearer + refresh tokens. The fiddly cookie sign-in and external-login reading live in
/// <c>FifthBox.Identity.AspNetCore</c>.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // Anonymous on purpose: the sign-in page reads this before anyone has an identity, so it can
        // hide a registration path the policy would refuse.
        group.MapGet("/registration", (IdentityOptions options) =>
            TypedResults.Ok(new Shared.Auth.RegistrationInfoResponse
            {
                SelfService = options.RegistrationPolicy == RegistrationPolicy.SelfService,
                InviteRequired = options.RegistrationPolicy == RegistrationPolicy.InviteOnly,
            })).AllowAnonymous();

        // --- Browser (cookie) flows ---
        // These bind the app's client-facing Shared DTOs and map to the Identity package's internal
        // contracts when calling the service (the WASM client only references /Shared).
        group.MapPost("/register", async (Shared.Auth.RegisterRequest request, IIdentityService svc, IUserClaimsFactory claims, IUserProfileStore profiles, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.RegisterAsync(
                new RegisterRequest { UserName = request.UserName, Password = request.Password, InviteCode = request.InviteCode },
                issueBearerTokens: false, ct);
            // Self-service register: the username is the email, so seed the profile from it.
            await UserProfileComposition.UpsertProfileAsync(profiles, result.UserId, result.UserName, string.Empty, string.Empty, ct);
            await IdentitySignIn.SignInCookieAsync(ctx, result, claims, CookieAuthenticationDefaults.AuthenticationScheme, ct);
            return TypedResults.Ok(result);
        }).AllowAnonymous();

        group.MapPost("/login", async (Shared.Auth.LoginRequest request, IIdentityService svc, IUserClaimsFactory claims, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.LoginAsync(
                new LoginRequest { UserName = request.UserName, Password = request.Password },
                issueBearerTokens: false, ct);
            await IdentitySignIn.SignInCookieAsync(ctx, result, claims, CookieAuthenticationDefaults.AuthenticationScheme, ct);
            return TypedResults.Ok(result);
        }).AllowAnonymous();

        group.MapPost("/logout", async (RefreshTokenRequest? request, IIdentityService svc, HttpContext ctx, CancellationToken ct) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!string.IsNullOrEmpty(request?.RefreshToken))
            {
                await svc.RevokeAsync(request.RefreshToken, ct);
            }
            return TypedResults.Ok();
        }).RequireAuthorization();

        // Note: the composed current-user view (identity + app profile) lives at /api/users/me;
        // account provisioning lives at POST /api/users. Both are in UserEndpoints.

        // --- Native (bearer) flows ---
        var native = group.MapGroup("/native");
        native.MapPost("/register", async (RegisterRequest request, IIdentityService svc, IUserProfileStore profiles, CancellationToken ct) =>
        {
            var result = await svc.RegisterAsync(request, issueBearerTokens: true, ct);
            await UserProfileComposition.UpsertProfileAsync(profiles, result.UserId, result.UserName, string.Empty, string.Empty, ct);
            return TypedResults.Ok(result);
        }).AllowAnonymous();
        native.MapPost("/login", async (LoginRequest request, IIdentityService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.LoginAsync(request, issueBearerTokens: true, ct))).AllowAnonymous();
        native.MapPost("/refresh", async (RefreshTokenRequest request, IIdentityService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.RefreshAsync(request.RefreshToken, ct))).AllowAnonymous();
        native.MapPost("/revoke", async (RefreshTokenRequest request, IIdentityService svc, CancellationToken ct) =>
        {
            await svc.RevokeAsync(request.RefreshToken, ct);
            return TypedResults.Ok();
        }).RequireAuthorization();

        // --- External provider (web OAuth) — signs a browser cookie ---
        group.MapGet("/external/{provider}/challenge", (string provider, string? returnUrl) =>
        {
            var callback = $"/api/auth/external/{provider}/callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";
            return Results.Challenge(new AuthenticationProperties { RedirectUri = callback }, [provider]);
        }).AllowAnonymous();

        group.MapGet("/external/{provider}/callback", async (string provider, string? returnUrl, IIdentityService svc, IUserClaimsFactory claims, IUserProfileStore profiles, HttpContext ctx, CancellationToken ct) =>
        {
            var info = await IdentitySignIn.ReadExternalLoginAsync(ctx, AuthenticationSetup.ExternalScheme, provider);
            if (info is null)
            {
                return Results.Redirect("/login?error=external_failed");
            }

            // The identity component links on (provider, key), or delegates to the app's resolver
            // (wired in Program.cs to match info.Email against the profile store), or — only when the
            // registration policy is SelfService — creates a new account.
            var result = await svc.LoginWithExternalAsync(info, issueBearerTokens: false, ct);

            // Persist/refresh the app profile from what the provider gave us.
            await UserProfileComposition.UpsertProfileAsync(profiles, result.UserId, info.Email, info.FirstName, info.LastName, ct);

            await ctx.SignOutAsync(AuthenticationSetup.ExternalScheme);
            await IdentitySignIn.SignInCookieAsync(ctx, result, claims, CookieAuthenticationDefaults.AuthenticationScheme, ct);
            return Results.LocalRedirect(returnUrl ?? "/");
        }).AllowAnonymous();

        return app;
    }
}
