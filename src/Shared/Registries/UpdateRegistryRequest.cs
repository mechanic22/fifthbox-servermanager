namespace FifthBox.ServerManager.Shared.Registries;

public record UpdateRegistryRequest
{
    public string Domain { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;

    /// Plaintext, write-only. Blank = keep the existing password.
    public string? Password { get; init; }

    public string? Prefix { get; init; }
}
