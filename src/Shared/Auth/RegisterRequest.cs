using System.ComponentModel.DataAnnotations;

namespace FifthBox.ServerManager.Shared.Auth;

/// <summary>
/// Self-service registration request from the browser client. (Host maps it to Identity's own
/// internal RegisterRequest.)
/// </summary>
public class RegisterRequest
{
    /// <summary>Login id — we use the email as the username.</summary>
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Only needed when registration policy is InviteOnly.</summary>
    public string? InviteCode { get; set; }
}
