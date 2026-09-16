namespace FifthBox.ServerManager.App.Platform;

/// anything holding ISecretProtector ciphertext implements this so the key can be checked and rotated
public interface IProtectedSecretStore
{
    /// shows in rotation errors, so name it for what the secrets are
    string Name { get; }

    /// any one ciphertext, enough to check the key still opens them
    Task<string?> SampleAsync(CancellationToken ct = default);

    /// builds the new state in memory first, a throwing rewrite leaves the store untouched
    Task RewriteAsync(Func<string, string> rewrite, CancellationToken ct = default);
}
