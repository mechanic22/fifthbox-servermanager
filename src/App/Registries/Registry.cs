namespace FifthBox.ServerManager.App.Registries;

/// password stored encrypted, never returned, only decrypted to auth a pull
public class Registry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Domain { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordEnc { get; set; } = string.Empty;

    /// optional image prefix that also routes here, on top of the domain
    public string? Prefix { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
