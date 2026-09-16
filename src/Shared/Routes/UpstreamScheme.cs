namespace FifthBox.ServerManager.Shared.Routes;

/// how nginx reaches the upstream, not how clients reach nginx
public enum UpstreamScheme
{
    Http,
    Https,
}
