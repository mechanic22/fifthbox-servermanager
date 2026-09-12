namespace FifthBox.ServerManager.Shared.Agents;

/// The plaintext enrollment key, shown once when an admin generates it. Only its hash is stored.
public record EnrollmentKeyResponse
{
    public required string Key { get; init; }
}
