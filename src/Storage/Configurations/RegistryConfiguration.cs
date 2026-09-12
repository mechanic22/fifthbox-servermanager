using FifthBox.ServerManager.App.Registries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FifthBox.ServerManager.Storage.Configurations;

public class RegistryConfiguration : IEntityTypeConfiguration<Registry>
{
    public void Configure(EntityTypeBuilder<Registry> builder)
    {
        builder.ToTable("Registries");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasMaxLength(64);
        builder.Property(r => r.Domain).IsRequired().HasMaxLength(253);
        builder.Property(r => r.Username).IsRequired().HasMaxLength(200);
        builder.Property(r => r.PasswordEnc).IsRequired().HasMaxLength(1000);
        builder.Property(r => r.Prefix).HasMaxLength(253);
        builder.HasIndex(r => r.Domain).IsUnique();
    }
}
