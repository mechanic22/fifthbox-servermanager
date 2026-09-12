using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Users;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// Typed HTTP client for admin-only user management. On failure it reads ProblemDetails and throws
/// <see cref="ApiException"/> — so a non-admin's 403 (Host enforcing the AdminOnly policy) shows up as
/// a friendly message, not a raw <see cref="HttpRequestException"/>.
/// </summary>
public interface IAdminClient
{
    /// <summary>Creates a user via <c>POST /api/users</c> (needs the admin role server-side).</summary>
    Task CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<UserSummaryResponse>> ListUsersAsync(CancellationToken ct = default);

    Task SetRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct = default);

    Task DeleteUserAsync(string userId, CancellationToken ct = default);
}

public sealed class AdminClient(HttpClient http) : IAdminClient
{
    public async Task CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/users", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw await HttpProblem.ToExceptionAsync(response, ct);
    }

    public async Task<IReadOnlyList<UserSummaryResponse>> ListUsersAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/users", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<UserSummaryResponse>>(ct) ?? [];
    }

    public async Task SetRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync(
            $"api/users/{userId}/roles", new UpdateUserRolesRequest { Roles = [.. roles] }, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    public async Task DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/users/{userId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }
}
