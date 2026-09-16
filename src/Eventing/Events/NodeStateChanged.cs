using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.Eventing.Events;

/// full snapshot, not a delta
public sealed record NodeStateChanged(IReadOnlyList<NodeResponse> Nodes);
