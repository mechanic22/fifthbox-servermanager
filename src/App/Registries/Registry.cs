namespace FifthBox.ServerManager.App.Registries;

/// A private Docker registry's pull credentials. The password is stored encrypted (PasswordEnc); it is
/// never returned to clients and only decrypted just-in-time to authenticate an image pull.
public class Registry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Domain { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordEnc { get; set; } = string.Empty;

    /// Optional image-name prefix that also routes to this registry (in addition to the domain).
    public string? Prefix { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
