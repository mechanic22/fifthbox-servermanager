using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Storage.Models;
using FifthBox.Identity.EntityFrameworkCore;
using FifthBox.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<IdentityUser> Users => Set<IdentityUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Workload> Workloads => Set<Workload>();
    public DbSet<WorkloadGroup> WorkloadGroups => Set<WorkloadGroup>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<EnrollmentKeyRecord> EnrollmentKeys => Set<EnrollmentKeyRecord>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<Registry> Registries => Set<Registry>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();
    public DbSet<Team> Teams => Set<Team>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyFifthBoxIdentityConfigurations();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
