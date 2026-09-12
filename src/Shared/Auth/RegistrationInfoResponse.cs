namespace FifthBox.ServerManager.Shared.Auth;

/// What the sign-in page is allowed to offer. The policy is enforced server-side either way; this
/// exists so the client doesn't show a "create account" path that always ends in a 403.
public sealed record RegistrationInfoResponse
{
    /// Anyone may create their own account.
    public bool SelfService { get; init; }

    /// Registration is open, but only with a valid invite code.
    public bool InviteRequired { get; init; }

    /// True when either self-service or invited registration is possible.
    public bool Allowed => SelfService || InviteRequired;
}
