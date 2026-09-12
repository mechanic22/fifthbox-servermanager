using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Platform;

/// Builds a protector for a key other than the configured one. Implemented in the composition root,
/// which is where the algorithm lives.
public interface ISecretProtectorFactory
{
    ISecretProtector ForKey(string key);
}

public interface ISecretCustodyService
{
    Task<SecretsState> CheckAsync(CancellationToken ct = default);

    /// Re-encrypt every stored secret under a new key. Returns how many values moved.
    Task<int> RotateAsync(string newKey, CancellationToken ct = default);
}

/// Looks after the encryption key: whether the configured one can still read what's stored, and moving
/// everything onto a new one.
public sealed class SecretCustodyService(
    IEnumerable<IProtectedSecretStore> stores,
    ISecretProtector protector,
    ISecretProtectorFactory factory,
    IOptions<EncryptionOptions> options) : ISecretCustodyService
{
    private const string Probe = "fbsm-probe";

    public async Task<SecretsState> CheckAsync(CancellationToken ct = default)
    {
        foreach (var store in stores)
        {
            if (await store.SampleAsync(ct) is not { } sample)
            {
                continue;
            }

            try
            {
                protector.Unprotect(sample);
                return SecretsState.Readable;
            }
            catch (Exception)
            {
                // Wrong key, or a value written under a since-lost ephemeral one. Either way the
                // operator needs telling rather than a 500 on the next image pull.
                return SecretsState.Unreadable;
            }
        }

        return SecretsState.NoSecrets;
    }

    public async Task<int> RotateAsync(string newKey, CancellationToken ct = default)
    {
        if (options.Value.Ephemeral)
        {
            throw new ConflictException("This Host is running on a generated key. Configure Platform:Encryption:Key and restart before rotating.");
        }

        var target = BuildAndVerify(newKey);

        // The same ciphertext always maps to the same new one. A workload's desired config and its
        // revisions hold identical encrypted bytes for an unchanged secret; re-encrypting each copy
        // separately would give them different bytes (fresh GCM nonce) and every workload would light up
        // as having pending changes the moment the key rotated.
        var rewrites = new Dictionary<string, string>(StringComparer.Ordinal);
        var failure = string.Empty;

        string Rewrite(string ciphertext)
        {
            if (rewrites.TryGetValue(ciphertext, out var replacement))
            {
                return replacement;
            }

            string plaintext;
            try
            {
                plaintext = protector.Unprotect(ciphertext);
            }
            catch (Exception)
            {
                throw new ConflictException($"A secret in {failure} can't be read with the current key.");
            }

            replacement = target.Protect(plaintext);
            rewrites[ciphertext] = replacement;
            return replacement;
        }

        foreach (var store in stores)
        {
            failure = store.Name;
            await store.RewriteAsync(Rewrite, ct);
        }

        return rewrites.Count;
    }

    // A round-trip proves the key is both well-formed and the right length — constructing a protector
    // alone doesn't, and finding out during rotation would leave secrets half-written.
    private ISecretProtector BuildAndVerify(string newKey)
    {
        if (string.IsNullOrWhiteSpace(newKey))
        {
            throw new ValidationException(nameof(newKey), "A new key is required.");
        }

        try
        {
            var target = factory.ForKey(newKey.Trim());
            if (target.Unprotect(target.Protect(Probe)) != Probe)
            {
                throw new ValidationException(nameof(newKey), "That key didn't round-trip a test value.");
            }

            return target;
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            throw new ValidationException(nameof(newKey), "That isn't a usable encryption key.");
        }
    }
}
