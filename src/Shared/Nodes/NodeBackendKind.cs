namespace FifthBox.ServerManager.Shared.Nodes;

/// How the app manages a node: a Docker Swarm member, or a custom agent (for non-swarm platforms).
public enum NodeBackendKind
{
    Swarm,
    Agent,
}
