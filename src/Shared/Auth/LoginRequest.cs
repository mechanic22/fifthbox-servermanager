using System.ComponentModel.DataAnnotations;

namespace FifthBox.ServerManager.Shared.Auth;

/// not Identity's LoginRequest, the host maps between them
public class LoginRequest
{
    /// the email, we use it as the username
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
