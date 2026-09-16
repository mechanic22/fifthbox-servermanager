using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.Identity;
using FifthBox.Identity.Abstractions;
using FifthBox.Identity.AspNetCore;
using FifthBox.Identity.Contracts;
using FifthBox.ServerManager.Storage.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FifthBox.ServerManager.Host.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // anonymous so the sign-in page can hide registration the policy would refuse
        group.MapGet("/registration", (IdentityOptions options) =>
            TypedResults.Ok(new Shared.Auth.RegistrationInfoResponse
            {
                SelfService = options.RegistrationPolicy == RegistrationPolicy.SelfService,
                InviteRequired = options.RegistrationPolicy == RegistrationPolicy.InviteOnly,
            })).AllowAnonymous();

        group.MapPost("/register", async (Shared.Auth.RegisterRequest request, IIdentityService svc, IUserClaimsFactory claims, IUserProfileStore profiles, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.RegisterAsync(
                new RegisterRequest { UserName = request.UserName, Password = request.Password, InviteCode = request.InviteCode },
                issueBearerTokens: false, ct);
            // self-service username is the email
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

            // links by provider key, then by profile email, and only creates an account under SelfService
            var result = await svc.LoginWithExternalAsync(info, issueBearerTokens: false, ct);

            await UserProfileComposition.UpsertProfileAsync(profiles, result.UserId, info.Email, info.FirstName, info.LastName, ct);

            await ctx.SignOutAsync(AuthenticationSetup.ExternalScheme);
            await IdentitySignIn.SignInCookieAsync(ctx, result, claims, CookieAuthenticationDefaults.AuthenticationScheme, ct);
            return Results.LocalRedirect(returnUrl ?? "/");
        }).AllowAnonymous();

        return app;
    }
}
