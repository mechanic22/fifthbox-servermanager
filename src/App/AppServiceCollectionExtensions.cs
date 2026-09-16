using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Workloads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FifthBox.ServerManager.App;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddApp(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IClusterState, ClusterState>();
        services.AddScoped<INodeService, NodeService>();
        services.AddScoped<INodeSource, AgentNodeSource>();
        services.AddScoped<IClusterService, ClusterService>();
        services.AddScoped<IWorkloadAccess, WorkloadAccess>();
        services.AddScoped<IWorkloadAudience, WorkloadAudience>();
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IWorkloadService, WorkloadService>();
        services.AddScoped<WorkloadDeploymentFactory>();
        services.AddScoped<WorkloadLoader>();
        services.AddScoped<WorkloadLifecycleService>();
        services.AddScoped<IWorkloadFileService, WorkloadFileService>();
        services.AddScoped<IWorkloadGroupService, WorkloadGroupService>();
        services.AddScoped<IWorkloadBackendResolver, WorkloadBackendResolver>();
        services.AddScoped<IRouteService, RouteService>();
        services.AddScoped<ICertificateService, CertificateService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IAgentDesiredState, AgentDesiredState>();
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        services.AddScoped<IHostDeploymentService, HostDeploymentService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<ISecretCustodyService, SecretCustodyService>();
        services.AddScoped<IScheduledJob, BackupJob>();
        services.AddScoped<IScheduledJob, ScheduledRestartJob>();
        services.AddScoped<IScheduledJob, CertificateRenewalJob>();
        // one instance backs both the CRUD surface and the image auth resolver
        services.AddScoped<RegistryService>();
        services.AddScoped<IRegistryService>(sp => sp.GetRequiredService<RegistryService>());
        services.AddScoped<IRegistryAuthResolver>(sp => sp.GetRequiredService<RegistryService>());
        return services;
    }
}
