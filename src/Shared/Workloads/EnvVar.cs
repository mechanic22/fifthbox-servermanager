namespace FifthBox.ServerManager.Shared.Workloads;

/// An environment variable. When <paramref name="Secret"/> is set, the stored Value is ciphertext and
/// responses carry it blank — the plaintext only ever exists on the way in and on the way to a backend.
public record EnvVar(string Key, string Value, bool Secret = false);
