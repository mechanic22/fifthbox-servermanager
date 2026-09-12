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
    /// <summary>
    /// Wires up Storage (owner of the app's SQLite database). Registers the DbContext plus every
    /// store — the identity stores (whose data Storage owns, implementing the Identity package's
    /// interfaces) and the profile sidecar. Domain repositories (the App layer's ports) are added
    /// here as each Manager lands. TryAdd so a test can swap any of them.
    /// </summary>
    public static IServiceCollection AddStorage(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        services.TryAddScoped<IUserProfileStore, EfUserProfileStore>();

        // Identity spine — Storage owns this data (see UserProfile's sidecar relationship). The EF
        // stores now come from FifthBox.Identity.EntityFrameworkCore instead of being hand-written.
        services.AddIdentityEntityFrameworkCore<AppDbContext>();

        // Domain persistence — the App layer's ports.
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

        // Every store holding encrypted values, so key rotation can reach all of them.
        services.AddScoped<IProtectedSecretStore, RegistrySecretStore>();
        services.AddScoped<IProtectedSecretStore, WorkloadSecretStore>();
        services.AddScoped<IProtectedSecretStore, AcmeAccountSecretStore>();
        services.AddScoped<IProtectedSecretStore, CertificateSecretStore>();

        return services;
    }

    /// <summary>
    /// Applies pending migrations. Migration code lives here in Storage; the Host only triggers this
    /// at startup. Creates its own scope so it can run before the request pipeline exists.
    /// </summary>
    public static async Task MigrateStorageAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
