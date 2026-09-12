namespace FifthBox.ServerManager.Shared.Nodes;

public record SetNodeRoleRequest
{
    public NodeRole Role { get; init; }
}
