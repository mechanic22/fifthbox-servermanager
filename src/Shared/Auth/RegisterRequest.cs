using System.ComponentModel.DataAnnotations;

namespace FifthBox.ServerManager.Shared.Auth;

/// not Identity's RegisterRequest, the host maps between them
public class RegisterRequest
{
    /// the email, we use it as the username
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    /// only needed for InviteOnly
    public string? InviteCode { get; set; }
}
