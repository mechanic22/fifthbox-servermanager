using FifthBox.ServerManager.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Hubs;

/// Server→client push of cluster node state. No client-callable methods — updates originate from the
/// ClusterStateWriter via Eventing and are broadcast by NodeStateBroadcaster. Admin-only, matching the
/// node endpoints: the policy on the hub is what keeps a broadcast-to-all off non-admin connections.
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class NodeHub : Hub;
