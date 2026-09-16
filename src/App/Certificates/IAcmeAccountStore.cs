namespace FifthBox.ServerManager.App.Certificates;

/// one account for the platform, losing it means re-registering, issued certs survive
public interface IAcmeAccountStore
{
    Task<StoredAcmeAccount?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(StoredAcmeAccount account, CancellationToken ct = default);
}

/// directory lives with the key, a staging key against prod fails as "account does not exist"
public sealed record StoredAcmeAccount
{
    public required string Directory { get; init; }
    public required string EncryptedKeyPem { get; init; }
}
