using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Platform;

public interface ISecretProtectorFactory
{
    ISecretProtector ForKey(string key);
}

public interface ISecretCustodyService
{
    Task<SecretsState> CheckAsync(CancellationToken ct = default);

    /// returns how many values moved
    Task<int> RotateAsync(string newKey, CancellationToken ct = default);
}

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
                // wrong key or a lost ephemeral one, tell the operator instead of a 500 on the next pull
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

        // same ciphertext maps to the same new one, a fresh GCM nonce per copy would flag
        // every workload as pending changes after a rotation
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

    // round-trip proves the key works, finding out mid-rotation leaves secrets half-written
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
