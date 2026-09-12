using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.Shared.Nodes;
using DockerModels = Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Maps a raw Docker swarm node into the domain Node. Pure — this is the tested part of the
/// integration; the adapter around it (SwarmNodeSource) just calls Docker.
public static class SwarmNodeMapper
{
    public static Node ToNode(DockerModels.NodeListResponse n) => new()
    {
        Id = n.ID,
        Hostname = n.Description?.Hostname ?? string.Empty,
        Role = ParseRole(n.Spec?.Role),
        Status = ParseStatus(n.Status?.State),
        Availability = ParseAvailability(n.Spec?.Availability),
        Platform = ParsePlatform(n.Description?.Platform?.OS),
        Architecture = string.IsNullOrEmpty(n.Description?.Platform?.Architecture) ? null : n.Description!.Platform.Architecture,
        IsLeader = n.ManagerStatus?.Leader ?? false,
        EngineVersion = string.IsNullOrEmpty(n.Description?.Engine?.EngineVersion) ? null : n.Description!.Engine.EngineVersion,
        Address = string.IsNullOrEmpty(n.Status?.Addr) ? null : n.Status!.Addr,
        Backend = NodeBackendKind.Swarm,
    };

    private static NodeRole ParseRole(string? role) => role?.ToLowerInvariant() switch
    {
        "manager" => NodeRole.Manager,
        _ => NodeRole.Worker,
    };

    private static NodeStatus ParseStatus(string? state) => state?.ToLowerInvariant() switch
    {
        "ready" => NodeStatus.Ready,
        "down" => NodeStatus.Down,
        "disconnected" => NodeStatus.Disconnected,
        _ => NodeStatus.Unknown,
    };

    private static NodeAvailability ParseAvailability(string? availability) => availability?.ToLowerInvariant() switch
    {
        "active" => NodeAvailability.Active,
        "pause" => NodeAvailability.Pause,
        "drain" => NodeAvailability.Drain,
        _ => NodeAvailability.Unknown,
    };

    private static NodePlatform ParsePlatform(string? os) => os?.ToLowerInvariant() switch
    {
        "linux" => NodePlatform.Linux,
        "windows" => NodePlatform.Windows,
        _ => NodePlatform.Unknown,
    };
}
