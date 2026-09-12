namespace FifthBox.ServerManager.Shared.Auth;

/// <summary>A refresh token presented to swap for a new token pair at <c>/api/auth/native/refresh</c>.</summary>
public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
