using FifthBox.Encryption.Aes;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;

namespace FifthBox.ServerManager.Host.Composition;

public sealed class SecretProtectorFactory : ISecretProtectorFactory
{
    public ISecretProtector ForKey(string key) => new AesSecretProtector(new AesEncryption(key));
}
