namespace FifthBox.ServerManager.App.Platform;

/// A store that persists values encrypted with <c>ISecretProtector</c>. Every component holding one
/// implements this so the key can be checked and rotated without anything else knowing what they are.
public interface IProtectedSecretStore
{
    /// Named for what the secrets are, so a rotation failure says something useful.
    string Name { get; }

    /// Any one stored ciphertext, or null if there are none — enough to test whether the configured key
    /// still opens them.
    Task<string?> SampleAsync(CancellationToken ct = default);

    /// Pass every stored ciphertext through <paramref name="rewrite"/> and save. Building the new state
    /// in memory before saving means a rewrite that throws leaves the store untouched.
    Task RewriteAsync(Func<string, string> rewrite, CancellationToken ct = default);
}
