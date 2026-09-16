using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Storage.Abstractions;
using FifthBox.ServerManager.Storage.Repositories;
using FifthBox.ServerManager.Storage.Stores;
using FifthBox.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FifthBox.ServerManager.Storage;

public static class StorageServiceCollectionExtensions
{
    /// TryAdd so tests can swap any of these
    public static IServiceCollection AddStorage(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        services.TryAddScoped<IUserProfileStore, EfUserProfileStore>();

        services.AddIdentityEntityFrameworkCore<AppDbContext>();

        services.TryAddScoped<IWorkloadRepository, EfWorkloadRepository>();
        services.TryAddScoped<IWorkloadGroupRepository, EfWorkloadGroupRepository>();
        services.TryAddScoped<IRouteRepository, EfRouteRepository>();
        services.TryAddScoped<IAgentRepository, EfAgentRepository>();
        services.TryAddScoped<IEnrollmentKeyStore, EfEnrollmentKeyStore>();
        services.TryAddScoped<IPlatformSettingsRepository, EfPlatformSettingsRepository>();
        services.TryAddScoped<IRegistryRepository, EfRegistryRepository>();
        services.TryAddScoped<IBackupStore, SqliteBackupStore>();
        services.TryAddScoped<IAcmeAccountStore, AcmeAccountStore>();
        services.TryAddScoped<ICertificateRepository, EfCertificateRepository>();
        services.TryAddScoped<IAccessGrantRepository, EfAccessGrantRepository>();
        services.TryAddScoped<ITeamRepository, EfTeamRepository>();
        services.TryAddScoped<IUserDirectory, EfUserDirectory>();

        // every store with encrypted values, so key rotation reaches them all
        services.AddScoped<IProtectedSecretStore, RegistrySecretStore>();
        services.AddScoped<IProtectedSecretStore, WorkloadSecretStore>();
        services.AddScoped<IProtectedSecretStore, AcmeAccountSecretStore>();
        services.AddScoped<IProtectedSecretStore, CertificateSecretStore>();

        return services;
    }

    /// makes its own scope so it can run before the request pipeline exists
    public static async Task MigrateStorageAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
