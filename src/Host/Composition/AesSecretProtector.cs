using FifthBox.Encryption.Aes;
using FifthBox.ServerManager.App.Registries;

namespace FifthBox.ServerManager.Host.Composition;

/// Bridges the App's ISecretProtector to FifthBox.Encryption.Aes (AES-256-GCM, random nonce per value).
public sealed class AesSecretProtector(IAesEncryption aes) : ISecretProtector
{
    public string Protect(string plaintext) => aes.Encrypt(plaintext);
    public string Unprotect(string ciphertext) => aes.Decrypt(ciphertext);
}
