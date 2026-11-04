using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Stats;

/// has to match ContainerStatsSignature on the Host, both test the same known value
public sealed record ReportEnvelope(byte[] Body, string Timestamp, string Signature)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ReportEnvelope Build(ContainerStatsReport report, string key, DateTimeOffset now)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(report, Json);
        var timestamp = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return new ReportEnvelope(body, timestamp, Sign(key, timestamp, body));
    }

    public static string Sign(string key, string timestamp, byte[] body)
    {
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(key));
        hmac.AppendData(Encoding.ASCII.GetBytes($"{timestamp}."));
        hmac.AppendData(body);
        return Convert.ToHexStringLower(hmac.GetHashAndReset());
    }
}
