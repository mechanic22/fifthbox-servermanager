using FifthBox.ServerManager.Shared.Users;

namespace FifthBox.ServerManager.Client.Core;

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
