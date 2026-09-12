namespace FifthBox.ServerManager.App.Platform;

/// Builds the shell command that promotes the Host from a plain container to a managed swarm service.
public static class InstallCommandRenderer
{
    /// Placeholders, never the live values — this ends up on a web page, and the operator already has
    /// the keys from when they first started the Host.
    private const string EncryptionKeyPlaceholder = "<your-encryption-key>";
    private const string JwtKeyPlaceholder = "<your-jwt-secret-key>";

    public static string Render(HostDeploymentOptions options, string? localNodeId)
    {
        // Stop first: swarm named volumes are node-local, so the service reuses the same volume this
        // container is holding. Two Hosts writing one SQLite file is the thing to avoid.
        var lines = new List<string>
        {
            $"docker rm -f {options.UnmanagedContainerName} \\",
            "  ; docker service create \\",
            $"      --name {options.ServiceName} \\",
            "      --replicas 1 \\",
        };

        // Pin to the node holding the volume. Without a resolved id, fall back to any manager — still
        // correct on a single-node swarm, which is where this matters most.
        lines.Add(string.IsNullOrWhiteSpace(localNodeId)
            ? "      --constraint node.role==manager \\"
            : $"      --constraint node.id=={localNodeId} \\");

        lines.AddRange(
        [
            $"      --publish published={options.PublishedPort},target={options.ContainerPort},mode=host \\",
            // On the overlay so nginx can reach the Host by service name — needed to route the UI
            // through it, and for the ACME challenge path later.
            $"      --network {options.OverlayNetwork} \\",
            "      --mount type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock \\",
            $"      --mount type=volume,src={options.DataVolume},dst={options.DataPath} \\",
            $"      --label {PlatformLabels.RoleKey}={PlatformLabels.PlatformRole} \\",
            // Swarm templates this at task launch, so its presence is what marks a managed install.
            "      --env FBSM_SERVICE_NAME='{{.Service.Name}}' \\",
            $"      --env Platform__Encryption__Key={EncryptionKeyPlaceholder} \\",
            $"      --env Identity__Jwt__SecretKey={JwtKeyPlaceholder} \\",
            $"      {options.Image}",
        ]);

        return string.Join('\n', lines);
    }
}
