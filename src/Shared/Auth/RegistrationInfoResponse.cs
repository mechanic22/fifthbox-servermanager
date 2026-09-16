namespace FifthBox.ServerManager.Shared.Auth;

/// so the ui doesn't offer a sign-up that just 403s, the server still enforces
public sealed record RegistrationInfoResponse
{
    public bool SelfService { get; init; }

    public bool InviteRequired { get; init; }

    public bool Allowed => SelfService || InviteRequired;
}
