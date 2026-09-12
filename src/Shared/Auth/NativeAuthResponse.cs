namespace FifthBox.ServerManager.Shared.Auth;

/// <summary>
/// The result of a native (bearer) sign-in: the account, plus the tokens the device stores. Mirrors
/// the Host's native auth response so the mobile client references only /Shared. Cookie flows don't
/// use this — the browser never sees a token.
/// </summary>
public class NativeAuthResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
