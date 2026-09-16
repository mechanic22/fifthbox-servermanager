using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Users;

namespace FifthBox.ServerManager.Client.Core;

public interface IAuthClient
{
    Task LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);

    /// null if not signed in
    Task<UserResponse?> GetCurrentUserAsync(CancellationToken ct = default);

    Task<RegistrationInfoResponse> GetRegistrationInfoAsync(CancellationToken ct = default);
}
