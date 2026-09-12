using System.ComponentModel.DataAnnotations;

namespace FifthBox.ServerManager.Shared.Auth;

/// <summary>
/// Login request the browser client sends. (Identity has its own internal LoginRequest — the Host
/// maps between them.)
/// </summary>
public class LoginRequest
{
    /// <summary>Login id — we use the email as the username.</summary>
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
