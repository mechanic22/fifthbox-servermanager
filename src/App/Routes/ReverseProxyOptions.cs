namespace FifthBox.ServerManager.App.Routes;

public sealed class ReverseProxyOptions
{
    public string ServiceName { get; set; } = "fbsm-nginx";
    public string Image { get; set; } = "nginx:1.27";
    public string ConfigPath { get; set; } = "/etc/nginx/conf.d/default.conf";

    /// the Host's own service on the overlay, defaults match HostDeploymentOptions
    public string AcmeUpstream { get; set; } = "fbsm-host:8080";

    /// published side only, nginx is always 80/443 inside the container
    /// moving http off 80 breaks cert issuance unless something forwards port 80 here
    public int HttpPort { get; set; } = 80;
    public int HttpsPort { get; set; } = 443;

    /// pinned is a single point of failure, global needs every node able to bind the ports and in dns
    /// off by default so an upgrade never reshapes a running edge
    public bool OnEveryNode { get; set; }
}

public static class ReverseProxyPorts
{
    public const int StandardHttp = 80;
    public const int StandardHttps = 443;

    /// "" when it's the scheme's default port
    public static string Suffix(int port, bool https) =>
        port == (https ? StandardHttps : StandardHttp) ? string.Empty : $":{port}";
}
