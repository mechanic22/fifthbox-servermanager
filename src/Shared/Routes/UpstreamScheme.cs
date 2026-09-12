namespace FifthBox.ServerManager.Shared.Routes;

/// The scheme nginx uses to reach the upstream. Unrelated to how clients reach nginx.
public enum UpstreamScheme
{
    Http,
    Https,
}
