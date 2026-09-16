using FifthBox.ServerManager.App.Workloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FifthBox.ServerManager.Storage.Configurations;

public class WorkloadGroupConfiguration : IEntityTypeConfiguration<WorkloadGroup>
{
    public void Configure(EntityTypeBuilder<WorkloadGroup> builder)
    {
        builder.ToTable("WorkloadGroups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasMaxLength(64);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(100);
        builder.Property(g => g.ParentId).HasMaxLength(64);
        // not unique on purpose, sqlite treats NULL ParentId as distinct so top-level dupes slip through
        // WorkloadGroupService enforces sibling names instead
        builder.HasIndex(g => g.ParentId);
    }
}
