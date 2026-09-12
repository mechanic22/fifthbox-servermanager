namespace FifthBox.ServerManager.App.Routes;

public sealed class ReverseProxyOptions
{
    public string ServiceName { get; set; } = "fbsm-nginx";
    public string Image { get; set; } = "nginx:1.27";
    public string ConfigPath { get; set; } = "/etc/nginx/conf.d/default.conf";

    /// Where nginx sends HTTP-01 challenges — the Host's own swarm service on the overlay. Defaults
    /// match HostDeploymentOptions (service name + container port).
    public string AcmeUpstream { get; set; } = "fbsm-host:8080";

    /// Ports the edge binds on its node. nginx always listens on 80/443 inside the container; these are
    /// the published side, for a machine where something else already owns the standard ports.
    /// Moving HTTP off 80 breaks certificate issuance unless something external forwards port 80 here —
    /// Let's Encrypt fetches the HTTP-01 challenge on port 80 and offers no way to say otherwise.
    public int HttpPort { get; set; } = 80;
    public int HttpsPort { get; set; } = 443;

    /// Run the edge on every node instead of pinning it to the one ServerManager talks to. Pinned is a
    /// single point of failure for every route — drain or lose that node and nothing answers — but
    /// global means every node has to be able to bind the ports above and every node's address has to be
    /// somewhere DNS points. Off by default so an upgrade never re-topologises a running edge.
    public bool OnEveryNode { get; set; }
}

public static class ReverseProxyPorts
{
    public const int StandardHttp = 80;
    public const int StandardHttps = 443;

    /// The ":port" a URL needs, or "" when it's the scheme's default and browsers imply it.
    public static string Suffix(int port, bool https) =>
        port == (https ? StandardHttps : StandardHttp) ? string.Empty : $":{port}";
}
