using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Users;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// The one place the app talks to the auth/user endpoints; pages and the auth state provider go
/// through this, never <see cref="HttpClient"/> inline. Two implementations satisfy it per head: the
/// web cookie client (signs a cookie, the browser carries it) and the native bearer client (stores
/// the token pair). Failures surface as <see cref="ApiException"/> with a user-safe message.
/// </summary>
public interface IAuthClient
{
    Task LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);

    /// <summary>The current user, or <c>null</c> if not signed in (401).</summary>
    Task<UserResponse?> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>Whether this server lets anyone create an account, so the UI can stop offering it.</summary>
    Task<RegistrationInfoResponse> GetRegistrationInfoAsync(CancellationToken ct = default);
}
