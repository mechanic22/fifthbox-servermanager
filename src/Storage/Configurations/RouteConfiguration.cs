using FifthBox.ServerManager.App.Routes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FifthBox.ServerManager.Storage.Configurations;

public class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("Routes");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasMaxLength(64);

        builder.Property(r => r.Hostname).IsRequired().HasMaxLength(253);
        builder.Property(r => r.Path).IsRequired().HasMaxLength(200);
        builder.Property(r => r.WorkloadId).HasMaxLength(64);
        builder.Property(r => r.Target).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.UpstreamHost).HasMaxLength(253);
        builder.Property(r => r.UpstreamScheme).HasConversion<string>().HasMaxLength(8);
        builder.HasIndex(r => r.WorkloadId);

        builder.Property(r => r.BasicAuthUsername).HasMaxLength(200);
        builder.Property(r => r.BasicAuthPasswordHash).HasMaxLength(200);
        // WebSockets / BasicAuthEnabled are bools — default column mapping.

        // One route per host+path.
        builder.Property(r => r.Enabled).HasDefaultValue(true);
        builder.HasIndex(r => new { r.Hostname, r.Path }).IsUnique();
    }
}
