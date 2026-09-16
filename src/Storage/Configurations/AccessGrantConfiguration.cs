using FifthBox.ServerManager.App.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FifthBox.ServerManager.Storage.Configurations;

public class AccessGrantConfiguration : IEntityTypeConfiguration<AccessGrant>
{
    public void Configure(EntityTypeBuilder<AccessGrant> builder)
    {
        builder.ToTable("AccessGrants");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasMaxLength(64);
        builder.Property(g => g.SubjectId).IsRequired().HasMaxLength(64);
        builder.Property(g => g.TargetId).IsRequired().HasMaxLength(64);
        builder.Property(g => g.SubjectType).HasConversion<string>().HasMaxLength(20);
        builder.Property(g => g.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(g => g.Level).HasConversion<string>().HasMaxLength(20);

        // one grant per subject per target, a re-grant is an update
        builder.HasIndex(g => new { g.SubjectType, g.SubjectId, g.Scope, g.TargetId }).IsUnique();
        // who can reach this target, and cleanup on delete
        builder.HasIndex(g => new { g.Scope, g.TargetId });
    }
}
