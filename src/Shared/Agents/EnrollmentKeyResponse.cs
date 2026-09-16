namespace FifthBox.ServerManager.Shared.Agents;

/// shown once, only the hash is stored
public record EnrollmentKeyResponse
{
    public required string Key { get; init; }
}
