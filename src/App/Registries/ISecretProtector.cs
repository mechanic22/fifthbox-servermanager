namespace FifthBox.ServerManager.App.Registries;

/// Reversible encryption for secrets at rest (registry passwords). Implemented in the Host composition
/// root — the algorithm and key live there, keeping App dependency-free.
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
