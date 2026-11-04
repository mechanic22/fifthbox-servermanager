using FifthBox.ServerManager.Shared.Certificates;

namespace FifthBox.ServerManager.Client.Web.Components;

public static class RouteUrl
{
    /// no valid cert still gets a link, just over http
    public static string For(string hostname, string path, CertificateStatus? tls, int edgeHttpPort, int edgeHttpsPort)
    {
        var https = tls == CertificateStatus.Valid;
        var port = https ? edgeHttpsPort : edgeHttpPort;
        var suffix = port == (https ? 443 : 80) ? string.Empty : $":{port}";
        return $"{(https ? "https" : "http")}://{hostname}{suffix}{path}";
    }
}
