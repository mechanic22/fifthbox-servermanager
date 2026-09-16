using System.ComponentModel.DataAnnotations;

namespace FifthBox.ServerManager.Shared.Users;

/// works whatever the registration policy, host splits it into an identity account + profile
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
