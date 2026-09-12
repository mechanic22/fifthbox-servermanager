namespace FifthBox.ServerManager.App.Certificates;

/// The ACME account, encrypted at rest. One account serves the whole platform: losing the key means
/// re-registering, not losing the certificates already issued.
public interface IAcmeAccountStore
{
    Task<StoredAcmeAccount?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(StoredAcmeAccount account, CancellationToken ct = default);
}

/// The directory travels with the key because an account only exists at the CA that issued it —
/// a staging key used against production fails as "account does not exist".
public sealed record StoredAcmeAccount
{
    public required string Directory { get; init; }
    public required string EncryptedKeyPem { get; init; }
}
