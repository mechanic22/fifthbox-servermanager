namespace FifthBox.ServerManager.Shared.Registries;

public record CreateRegistryRequest
{
    public string Domain { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;

    /// plaintext in, encrypted server-side, never returned
    public string? Password { get; init; }

    public string? Prefix { get; init; }
}
