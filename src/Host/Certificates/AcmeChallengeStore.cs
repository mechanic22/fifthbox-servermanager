using System.Collections.Concurrent;
using FifthBox.ServerManager.App.Certificates;

namespace FifthBox.ServerManager.Host.Certificates;

public sealed class AcmeChallengeStore : IAcmeChallengeStore
{
    private readonly ConcurrentDictionary<string, string> _pending = new(StringComparer.Ordinal);

    public void Publish(string token, string keyAuthorization) => _pending[token] = keyAuthorization;

    public string? Resolve(string token) => _pending.GetValueOrDefault(token);

    public void Clear(string token) => _pending.TryRemove(token, out _);
}
