using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Storage.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FifthBox.ServerManager.Storage.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasMaxLength(64);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Platform).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.SecretHash).IsRequired().HasMaxLength(64);
    }
}

public class EnrollmentKeyConfiguration : IEntityTypeConfiguration<EnrollmentKeyRecord>
{
    public void Configure(EntityTypeBuilder<EnrollmentKeyRecord> builder)
    {
        builder.ToTable("EnrollmentKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).HasMaxLength(32);
        builder.Property(k => k.Hash).IsRequired().HasMaxLength(64);
    }
}
