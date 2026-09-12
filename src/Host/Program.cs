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

// Validate the DI graph when the app is built, so a composition mistake (e.g. surfacing identity
// without registering the component that owns the identity stores) fails at startup rather than on
// the first request that resolves the affected service.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// Dev convenience: without a configured JWT signing key, use an ephemeral one so the app runs locally
// without committing a secret. Production must configure Identity:Jwt:SecretKey.
if (string.IsNullOrWhiteSpace(builder.Configuration["Identity:Jwt:SecretKey"]) && builder.Environment.IsDevelopment())
{
    builder.Configuration["Identity:Jwt:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}

// Registry credential encryption key. Dev generates an ephemeral one; production must set
// Platform:Encryption:Key (base64). NOTE: an ephemeral key can't decrypt secrets stored in a prior run.
var ephemeralEncryptionKey = string.IsNullOrWhiteSpace(builder.Configuration["Platform:Encryption:Key"]) && builder.Environment.IsDevelopment();
if (ephemeralEncryptionKey)
{
    builder.Configuration["Platform:Encryption:Key"] = AesGenerator.GenerateKey();
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The default known-proxy list is loopback only, so headers from the nginx container (a different
    // IP every deploy) would be dropped and every request would look like plain HTTP from the proxy.
    // Safe to trust any peer here: the Host is only ever reached through nginx or on a private network.
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddIdentity(options => builder.Configuration.GetSection("Identity").Bind(options));
// Core Identity ships only the IPasswordHasher interface; the FifthBox.Identity.BCrypt companion
// supplies the bcrypt algorithm. Work factor stays configurable.
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
// Route basic-auth htpasswd hashing — App defines the port, the Host picks bcrypt.
builder.Services.AddSingleton<IBasicAuthHasher, BasicAuthHasher>();

// Registry credential encryption — App defines ISecretProtector, the Host picks AES (FifthBox.Encryption.Aes).
builder.Services.AddAesEncryption(
    builder.Configuration["Platform:Encryption:Key"] ?? throw new InvalidOperationException("Platform:Encryption:Key is required."));
builder.Services.AddScoped<ISecretProtector, AesSecretProtector>();
builder.Services.AddSingleton<ISecretProtectorFactory, SecretProtectorFactory>();
builder.Services.Configure<EncryptionOptions>(o => o.Ephemeral = ephemeralEncryptionKey);
builder.Services.AddEventing();

// Realtime node state: the writer owns "refresh, then tell clients only if it changed"; the broadcaster
// bridges that event onto the NodeHub. The reconcile job is the periodic safety net under it.
builder.Services.AddSignalR(options => options.AddFilter<CanonicalExceptionHubFilter>());
builder.Services.AddSingleton<CanonicalExceptionHubFilter>();
builder.Services.AddScoped<IEventHandler<NodeStateChanged>, NodeStateBroadcaster>();
builder.Services.AddSingleton<IClusterStateWriter, ClusterStateWriter>();
builder.Services.AddScoped<IScheduledJob, ClusterReconcileJob>();
builder.Services.AddHostedService<SwarmEventListener>();

// One runner drives every IScheduledJob; it doubles as the status/run-now surface for the API.
builder.Services.AddSingleton<ScheduledJobRunner>();
builder.Services.AddSingleton<IScheduledJobs>(sp => sp.GetRequiredService<ScheduledJobRunner>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ScheduledJobRunner>());

// Agents push supervision transitions as they happen; the relay resolves the workload and raises the
// event, and the broadcaster puts it on the workload hub.
builder.Services.AddSingleton<IWorkloadLogFollower, WorkloadLogFollower>();
builder.Services.AddScoped<IAgentStatusRelay, AgentStatusRelay>();
builder.Services.AddScoped<IEventHandler<WorkloadStatusChanged>, WorkloadStatusBroadcaster>();

// Agent connections: the registry tracks live agent connections for the AgentHub; the command channel
// dispatches workload commands to them; AddAgents registers the native workload backend.
builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();
builder.Services.AddScoped<IAgentCommandChannel, AgentCommandChannel>();
builder.Services.AddAgents();

builder.Services.AddSingleton<IAcmeChallengeStore, AcmeChallengeStore>();

// Auth cookies and antiforgery tokens are encrypted with these keys. Left unconfigured the framework
// keeps them under $HOME, which in a container is the writable layer — so every restart would issue new
// keys and sign everybody out. The path is a volume in the image; relative in dev, like the database.
var keyPath = Path.Combine(builder.Environment.ContentRootPath,
    builder.Configuration.GetValue("DataProtection:KeyPath", "keys")!);
Directory.CreateDirectory(keyPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    // Pinned: the default discriminator includes the content root path, so keys stop working the moment
    // the app moves directory — which a container image change can do on its own.
    .SetApplicationName("FifthBox.ServerManager");

builder.Services.AddAppAuthentication(builder.Configuration);
builder.Services.AddAuthorizationBuilder().AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(Roles.Admin));
builder.Services.AddAntiforgery();

var app = builder.Build();

// Attach a first-time external (e.g. Google) login to a pre-provisioned account by profile email.
app.UseExternalLoginProfileResolver();

if (builder.Configuration.GetValue("Identity:AllowInsecureCookies", false))
{
    app.Logger.LogWarning(
        "Identity:AllowInsecureCookies is on — the session cookie will be sent over plain HTTP and is " +
        "readable by anything on the network path. Turn this off once the host is behind TLS.");
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

// MapStaticAssets, not UseStaticFiles: fingerprinted names exist only as routes in the build's asset
// manifest, never as files on disk, so the file-based middleware 404s every one of them — including
// every JS module imported through the importmap the build writes into index.html. UseBlazorFrameworkFiles
// is gone with it: it branches the pipeline for /_framework, and a branch that starts after routing has
// already picked a static-asset endpoint ends without ever executing it.
app.MapStaticAssets();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Liveness only — deliberately no database probe. A migration or disk hiccup would fail the check,
// get the container killed, and restart it straight back into the same hiccup.
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

// Storage owns the migrations; the Host only triggers them. Then bootstrap the first admin from
// Identity:DemoSeedAdmin config (no-op when unconfigured).
await app.Services.BackupBeforeMigrationsAsync(app.Logger);
await app.Services.MigrateStorageAsync();
await app.SeedDemoSeedAdminAsync();

app.Run();

// Named so WebApplicationFactory can boot this exact pipeline in tests; top-level statements otherwise
// generate an internal entry point no test project can reach.
public partial class Program;
