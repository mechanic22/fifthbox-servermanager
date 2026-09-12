using System.ComponentModel.DataAnnotations;

namespace FifthBox.ServerManager.Shared.Users;

/// <summary>
/// Admin request to create a user. Bundles the identity bits (username, password, roles) and the
/// profile bits (email, name); Host splits it into an identity account plus a stored profile.
/// Works no matter the registration policy.
/// </summary>
public class CreateUserRequest
{
    [Required]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
}
