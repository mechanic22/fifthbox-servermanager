using FifthBox.ServerManager.Shared.Users;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// UI-agnostic auth session for non-Blazor heads (native MAUI). Holds the current user and raises
/// <see cref="StateChanged"/> so views can rebind. The web head uses Blazor's
/// <c>AuthenticationStateProvider</c> instead — this exists so the bearer pipeline has a UI-neutral
/// place to reflect sign-in and signal sign-out.
/// </summary>
public interface IAuthSession
{
    UserResponse? CurrentUser { get; }
    bool IsSignedIn { get; }
    event Action? StateChanged;
    void SetUser(UserResponse? user);
    Task SignOutAsync();
}

public sealed class AuthSession : IAuthSession
{
    public UserResponse? CurrentUser { get; private set; }
    public bool IsSignedIn => CurrentUser is not null;
    public event Action? StateChanged;

    public void SetUser(UserResponse? user)
    {
        CurrentUser = user;
        StateChanged?.Invoke();
    }

    public Task SignOutAsync()
    {
        SetUser(null);
        return Task.CompletedTask;
    }
}
