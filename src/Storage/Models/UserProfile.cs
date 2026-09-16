namespace FifthBox.ServerManager.Storage.Models;

public class UserProfile
{
    /// IdentityUser.Id, also the primary key
    public string UserId { get; set; } = string.Empty;

    /// contact email, not the login id (though usually the same)
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
