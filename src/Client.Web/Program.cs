using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Client.Web;
using FifthBox.ServerManager.Client.Web.Services;
using FifthBox.ServerManager.Shared.Auth;
using MaterialComponents.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin: the Host serves this app, so requests go back to it. Cookie auth rides along
// automatically; the client never handles a token.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMaterialComponents();

// Register the same named policies the Host enforces (from the shared constants) so AuthorizeView and
// [Authorize(Policy = ...)] evaluate identically to the server side.
builder.Services.AddAuthorizationCore(options =>
    options.AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(Roles.Admin)));

builder.Services.AddScoped<IAuthClient, AuthClient>();
builder.Services.AddScoped<IAdminClient, AdminClient>();
builder.Services.AddScoped<IAccessClient, AccessClient>();
builder.Services.AddScoped<ITeamsClient, TeamsClient>();
builder.Services.AddScoped<INodesClient, NodesClient>();
builder.Services.AddScoped<IClusterClient, ClusterClient>();
builder.Services.AddScoped<IWorkloadsClient, WorkloadsClient>();
builder.Services.AddScoped<IWorkloadGroupsClient, WorkloadGroupsClient>();
builder.Services.AddScoped<IRoutesClient, RoutesClient>();
builder.Services.AddScoped<IAgentsClient, AgentsClient>();
builder.Services.AddScoped<ITlsClient, TlsClient>();
builder.Services.AddScoped<ISystemClient, SystemClient>();
builder.Services.AddScoped<IRegistriesClient, RegistriesClient>();

// One HubConnection for the whole app, owned by this service.
builder.Services.AddScoped<RealtimeService>();

// One instance exposed under both the concrete type (for login/logout to notify) and the framework
// abstraction.
builder.Services.AddScoped<CookieAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthenticationStateProvider>());

await builder.Build().RunAsync();
