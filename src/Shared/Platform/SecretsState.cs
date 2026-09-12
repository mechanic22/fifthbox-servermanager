namespace FifthBox.ServerManager.Shared.Platform;

public enum SecretsState
{
    /// Nothing encrypted is stored yet, so the key hasn't been proven either way.
    NoSecrets,

    Readable,

    /// Stored secrets won't decrypt with the configured key.
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
