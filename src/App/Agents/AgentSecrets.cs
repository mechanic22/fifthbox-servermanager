using System.Security.Cryptography;
using System.Text;

namespace FifthBox.ServerManager.App.Agents;

/// high-entropy tokens not passwords, so sha-256 + fixed-time compare (bcrypt is for passwords)
public static class AgentSecrets
{
    public static string Generate() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string secret) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public static bool Verify(string secret, string? hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return false;
        }

        Span<byte> computed = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(secret), computed);

        Span<byte> stored = stackalloc byte[32];
        return Convert.TryFromBase64String(hash, stored, out var written)
            && written == 32
            && CryptographicOperations.FixedTimeEquals(computed, stored);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
