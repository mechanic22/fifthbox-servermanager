namespace FifthBox.ServerManager.Storage.Models;

/// <summary>
/// A user's app profile — the sidecar to the identity account. Owned by Storage, keyed by the
/// identity id (<see cref="UserId"/> == <c>IdentityUser.Id</c>). Holds the app stuff Identity
/// deliberately doesn't: contact email, display name. Identity never sees this.
/// </summary>
public class UserProfile
{
    /// <summary>The identity account id this profile belongs to. Primary key.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Contact email (separate from the login id, though often the same).</summary>
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
