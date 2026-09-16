namespace FifthBox.ServerManager.Shared.Platform;

public enum SecretsState
{
    /// nothing encrypted yet, so the key's unproven
    NoSecrets,

    Readable,

    /// won't decrypt with the configured key
    Unreadable,
}

public record RotateEncryptionKeyRequest
{
    public string NewKey { get; init; } = string.Empty;
}

public record RotateEncryptionKeyResponse
{
    public int SecretsRewritten { get; init; }
}
