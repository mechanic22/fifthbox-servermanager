namespace FifthBox.ServerManager.Shared.Registries;

public record RegistryResponse
{
    public required string Id { get; init; }
    public required string Domain { get; init; }
    public required string Username { get; init; }
    public string? Prefix { get; init; }
}
