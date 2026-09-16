using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using FifthBox.ServerManager.App;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.Eventing;
using FifthBox.ServerManager.Eventing.Events;
using FifthBox.ServerManager.Host.Agents;
using FifthBox.ServerManager.Host.Certificates;
using FifthBox.ServerManager.Host.Endpoints;
using FifthBox.ServerManager.Host.Hubs;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.Host.Composition;
using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.Encryption.Aes;
using FifthBox.ServerManager.Host.Realtime;
using FifthBox.ServerManager.Integrations.Agents;
using FifthBox.ServerManager.Integrations.Certificates;
using FifthBox.ServerManager.Integrations.ReverseProxy;
using FifthBox.ServerManager.Integrations.Swarm;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Storage;
using FifthBox.Identity;
using FifthBox.Identity.BCrypt;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// wiring mistakes blow up at startup instead of on the first request
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

if (string.IsNullOrWhiteSpace(builder.Configuration["Identity:Jwt:SecretKey"]) && builder.Environment.IsDevelopment())
{
    builder.Configuration["Identity:Jwt:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}

// NOTE: dev throwaway key can't decrypt secrets saved by a previous run. prod must set Platform:Encryption:Key
var ephemeralEncryptionKey = string.IsNullOrWhiteSpace(builder.Configuration["Platform:Encryption:Key"]) && builder.Environment.IsDevelopment();
if (ephemeralEncryptionKey)
{
    builder.Configuration["Platform:Encryption:Key"] = AesGenerator.GenerateKey();
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // nginx gets a new ip every deploy so trust any proxy. host is only reachable through nginx anyway
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddIdentity(options => builder.Configuration.GetSection("Identity").Bind(options));
builder.Services.AddBcryptPasswordHasher(builder.Configuration.GetValue("Identity:PasswordHasher:WorkFactor", 12));

var connectionString = builder.Configuration.GetConnectionString("AppDb") ?? "Data Source=fifth-box-server-manager.db";
builder.Services.AddStorage(connectionString);
builder.Services.AddApp();
builder.Services.AddSwarm(builder.Configuration["Docker:Endpoint"] ?? "unix:///var/run/docker.sock");
builder.Services.Configure<ClusterOptions>(builder.Configuration.GetSection("Cluster"));
builder.Services.Configure<HostDeploymentOptions>(builder.Configuration.GetSection("HostDeployment"));
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection("Backup"));
builder.Services.AddSingleton<IHostProcessInfo, HostProcessInfo>();
builder.Services.Configure<ReverseProxyOptions>(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddReverseProxy();
builder.Services.Configure<AcmeOptions>(builder.Configuration.GetSection("Acme"));
builder.Services.AddCertificates();
builder.Services.AddSingleton<IBasicAuthHasher, BasicAuthHasher>();

builder.Services.AddAesEncryption(
    builder.Configuration["Platform:Encryption:Key"] ?? throw new InvalidOperationException("Platform:Encryption:Key is required."));
builder.Services.AddScoped<ISecretProtector, AesSecretProtector>();
builder.Services.AddSingleton<ISecretProtectorFactory, SecretProtectorFactory>();
builder.Services.Configure<EncryptionOptions>(o => o.Ephemeral = ephemeralEncryptionKey);
builder.Services.AddEventing();

builder.Services.AddSignalR(options => options.AddFilter<CanonicalExceptionHubFilter>());
builder.Services.AddSingleton<CanonicalExceptionHubFilter>();
builder.Services.AddScoped<IEventHandler<NodeStateChanged>, NodeStateBroadcaster>();
builder.Services.AddSingleton<IClusterStateWriter, ClusterStateWriter>();
builder.Services.AddScoped<IScheduledJob, ClusterReconcileJob>();
builder.Services.AddHostedService<SwarmEventListener>();

// same instance runs the jobs and backs the status/run-now api
builder.Services.AddSingleton<ScheduledJobRunner>();
builder.Services.AddSingleton<IScheduledJobs>(sp => sp.GetRequiredService<ScheduledJobRunner>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ScheduledJobRunner>());

builder.Services.AddSingleton<IWorkloadLogFollower, WorkloadLogFollower>();
builder.Services.AddScoped<IAgentStatusRelay, AgentStatusRelay>();
builder.Services.AddScoped<IEventHandler<WorkloadStatusChanged>, WorkloadStatusBroadcaster>();

builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();
builder.Services.AddScoped<IAgentCommandChannel, AgentCommandChannel>();
builder.Services.AddAgents();

builder.Services.AddSingleton<IAcmeChallengeStore, AcmeChallengeStore>();

// default key dir is $HOME, which a container loses on restart and signs everyone out. this path is a volume
var keyPath = Path.Combine(builder.Environment.ContentRootPath,
    builder.Configuration.GetValue("DataProtection:KeyPath", "keys")!);
Directory.CreateDirectory(keyPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    // pinned, the default includes the content root so keys break if the app dir moves
    .SetApplicationName("FifthBox.ServerManager");

builder.Services.AddAppAuthentication(builder.Configuration);
builder.Services.AddAuthorizationBuilder().AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(Roles.Admin));
builder.Services.AddAntiforgery();

var app = builder.Build();

app.UseExternalLoginProfileResolver();

if (builder.Configuration.GetValue("Identity:AllowInsecureCookies", false))
{
    app.Logger.LogWarning(
        "Identity:AllowInsecureCookies is on — the session cookie will be sent over plain HTTP and is " +
        "readable by anything on the network path. Turn this off once the host is behind TLS.");
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

// not UseStaticFiles, fingerprinted assets only exist as manifest routes so it 404s them
// and no UseBlazorFrameworkFiles either, its /_framework branch swallows the static-asset endpoint
app.MapStaticAssets();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// liveness only, a db probe would get the container killed over a hiccup and restart into the same hiccup
app.MapHealthChecks("/healthz").AllowAnonymous();

app.MapCertificateEndpoints();

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapAccessEndpoints();
app.MapTeamEndpoints();
app.MapNodeEndpoints();
app.MapClusterEndpoints();
app.MapWorkloadEndpoints();
app.MapWorkloadGroupEndpoints();
app.MapRouteEndpoints();
app.MapSystemEndpoints();
app.MapRegistryEndpoints();
app.MapAgentEndpoints();
app.MapHub<NodeHub>("/hubs/nodes");
app.MapHub<AgentHub>("/hubs/agents");
app.MapHub<WorkloadHub>("/hubs/workloads");
app.MapFallbackToFile("index.html");

await app.Services.BackupBeforeMigrationsAsync(app.Logger);
await app.Services.MigrateStorageAsync();
await app.SeedDemoSeedAdminAsync();

app.Run();

// public so WebApplicationFactory can reach it from tests
public partial class Program;
