namespace FifthBox.ServerManager.App.Platform;

public static class InstallCommandRenderer
{
    /// placeholders on purpose, this ends up on a web page
    private const string EncryptionKeyPlaceholder = "<your-encryption-key>";
    private const string JwtKeyPlaceholder = "<your-jwt-secret-key>";

    public static string Render(HostDeploymentOptions options, string? localNodeId)
    {
        // stop first, named volumes are node-local and two Hosts on one sqlite file is bad
        var lines = new List<string>
        {
            $"docker rm -f {options.UnmanagedContainerName} \\",
            "  ; docker service create \\",
            $"      --name {options.ServiceName} \\",
            "      --replicas 1 \\",
        };

        // pin to the volume's node, any manager if unresolved (fine on a single node)
        lines.Add(string.IsNullOrWhiteSpace(localNodeId)
            ? "      --constraint node.role==manager \\"
            : $"      --constraint node.id=={localNodeId} \\");

        lines.AddRange(
        [
            $"      --publish published={options.PublishedPort},target={options.ContainerPort},mode=host \\",
            // overlay so nginx can reach the Host by service name
            $"      --network {options.OverlayNetwork} \\",
            "      --mount type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock \\",
            $"      --mount type=volume,src={options.DataVolume},dst={options.DataPath} \\",
            $"      --label {PlatformLabels.RoleKey}={PlatformLabels.PlatformRole} \\",
            // swarm templates this at launch, its presence marks a managed install
            "      --env FBSM_SERVICE_NAME='{{.Service.Name}}' \\",
            $"      --env Platform__Encryption__Key={EncryptionKeyPlaceholder} \\",
            $"      --env Identity__Jwt__SecretKey={JwtKeyPlaceholder} \\",
            $"      {options.Image}",
        ]);

        return string.Join('\n', lines);
    }
}
