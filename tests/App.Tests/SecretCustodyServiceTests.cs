using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class SecretCustodyServiceTests
{
    /// Reversible stand-in for AES: prefixes with the key, so a value written under one key visibly
    /// fails to open under another.
    private sealed class KeyedProtector(string key) : ISecretProtector
    {
        public string Protect(string plaintext) => $"{key}:{plaintext}";

        public string Unprotect(string ciphertext) =>
            ciphertext.StartsWith($"{key}:", StringComparison.Ordinal)
                ? ciphertext[(key.Length + 1)..]
                : throw new InvalidOperationException("wrong key");
    }

    /// Stands in for a store holding the given ciphertexts, applying the rewrite in place.
    private sealed class FakeStore(params string[] stored) : IProtectedSecretStore
    {
        public List<string> Values { get; } = [.. stored];
        public bool Rewritten { get; private set; }

        public string Name => "registry credentials";

        public Task<string?> SampleAsync(CancellationToken ct = default) => Task.FromResult(Values.FirstOrDefault());

        public Task RewriteAsync(Func<string, string> rewrite, CancellationToken ct = default)
        {
            var updated = Values.Select(rewrite).ToList();
            Values.Clear();
            Values.AddRange(updated);
            Rewritten = true;
            return Task.CompletedTask;
        }
    }

    private static (SecretCustodyService svc, FakeStore store) Build(
        string currentKey = "old", bool ephemeral = false, params string[] stored)
    {
        var store = new FakeStore(stored);

        var factory = new Mock<ISecretProtectorFactory>();
        factory.Setup(f => f.ForKey(It.IsAny<string>())).Returns((string k) => new KeyedProtector(k));

        var svc = new SecretCustodyService(
            [store],
            new KeyedProtector(currentKey),
            factory.Object,
            Options.Create(new EncryptionOptions { Ephemeral = ephemeral }));

        return (svc, store);
    }

    [TestMethod]
    public async Task No_stored_secrets_proves_nothing_either_way()
    {
        var (svc, _) = Build();

        Assert.AreEqual(SecretsState.NoSecrets, await svc.CheckAsync());
    }

    [TestMethod]
    public async Task Readable_when_the_configured_key_opens_a_stored_secret()
    {
        var (svc, _) = Build("old", false, "old:hunter2");

        Assert.AreEqual(SecretsState.Readable, await svc.CheckAsync());
    }

    [TestMethod]
    public async Task Unreadable_when_it_does_not()
    {
        var (svc, _) = Build("current", false, "previous:hunter2");

        Assert.AreEqual(SecretsState.Unreadable, await svc.CheckAsync());
    }

    [TestMethod]
    public async Task Rotate_rewrites_every_secret_under_the_new_key()
    {
        var (svc, store) = Build("old", false, "old:hunter2", "old:letmein");

        var count = await svc.RotateAsync("new");

        Assert.AreEqual(2, count);
        CollectionAssert.AreEquivalent(new[] { "new:hunter2", "new:letmein" }, store.Values);
    }

    [TestMethod]
    public async Task Rotate_writes_nothing_when_a_secret_cannot_be_read_first()
    {
        var (svc, store) = Build("current", false, "current:fine", "previous:orphaned");

        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.RotateAsync("new"));

        CollectionAssert.Contains(store.Values, "current:fine", "the store must not be left half-rotated");
    }

    [TestMethod]
    public async Task Rotate_is_refused_while_running_on_a_generated_key()
    {
        var (svc, _) = Build("old", ephemeral: true);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.RotateAsync("new"));
    }

    [TestMethod]
    public async Task The_same_secret_stored_twice_rotates_to_the_same_ciphertext()
    {
        // Desired config and its revisions share bytes; if rotation gave them different ones, every
        // workload would show pending changes straight after a rotation.
        var (svc, store) = Build("old", false, "old:hunter2", "old:hunter2");

        await svc.RotateAsync("new");

        Assert.AreEqual(store.Values[0], store.Values[1]);
    }

    [TestMethod]
    public async Task Rotate_rejects_an_empty_key()
    {
        var (svc, _) = Build();

        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.RotateAsync("  "));
    }
}
