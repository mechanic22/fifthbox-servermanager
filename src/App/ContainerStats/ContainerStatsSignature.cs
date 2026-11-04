using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FifthBox.ServerManager.App.ContainerStats;

/// the overlay isn't encrypted, so reports are signed and the key never goes on the wire
public static class ContainerStatsSignature
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(60);

    private static readonly byte[] KeyLabel = "fbsm-container-stats-v1"u8.ToArray();

    /// one-way, so this key can't get back to the encryption key
    public static string DeriveKey(string encryptionKey) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey), KeyLabel));

    public static string Sign(string key, long unixSeconds, ReadOnlySpan<byte> body)
    {
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(key));
        hmac.AppendData(Encoding.ASCII.GetBytes($"{unixSeconds.ToString(CultureInfo.InvariantCulture)}."));
        hmac.AppendData(body);
        return Convert.ToHexStringLower(hmac.GetHashAndReset());
    }

    public static bool IsValid(string key, string? timestamp, string? signature, ReadOnlySpan<byte> body, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(signature)
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds)
            || unixSeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return false;
        }

        var age = now - DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if (age.Duration() > MaxAge)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Sign(key, unixSeconds, body)),
            Encoding.ASCII.GetBytes(signature));
    }
}
