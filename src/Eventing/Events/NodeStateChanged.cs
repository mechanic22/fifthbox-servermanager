using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.Eventing.Events;

/// The observed cluster nodes changed — added, removed, or a status/availability shift. Carries the
/// full current snapshot; consumers replace their view rather than applying a delta.
public sealed record NodeStateChanged(IReadOnlyList<NodeResponse> Nodes);
