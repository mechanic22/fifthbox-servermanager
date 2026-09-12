namespace FifthBox.ServerManager.Shared.Registries;

/// A registry's non-secret details. The password is never returned.
public record RegistryResponse
{
    public required string Id { get; init; }
    public required string Domain { get; init; }
    public required string Username { get; init; }
    public string? Prefix { get; init; }
}
