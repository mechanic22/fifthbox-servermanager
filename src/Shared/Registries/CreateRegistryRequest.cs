namespace FifthBox.ServerManager.Shared.Registries;

public record CreateRegistryRequest
{
    public string Domain { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;

    /// Plaintext, write-only — encrypted server-side, never stored or returned as plaintext.
    public string? Password { get; init; }

    public string? Prefix { get; init; }
}
