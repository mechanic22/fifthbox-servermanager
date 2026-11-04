namespace FifthBox.ServerManager.Client.Web.Services;

/// latest value per key, gone once it's too old to trust. pushes just stop when a node goes quiet
public sealed class RecentValues<T> where T : class
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, (T Value, DateTimeOffset At)> _values = new(StringComparer.Ordinal);

    public void Set(string key, T value, DateTimeOffset now) => _values[key] = (value, now);

    public T? Get(string key, DateTimeOffset now) =>
        _values.TryGetValue(key, out var entry) && now - entry.At <= MaxAge ? entry.Value : null;
}
