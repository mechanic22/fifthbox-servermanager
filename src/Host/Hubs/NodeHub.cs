using FifthBox.ServerManager.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Hubs;

/// this policy is what keeps the broadcast-to-all off non-admin connections
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class NodeHub : Hub;
