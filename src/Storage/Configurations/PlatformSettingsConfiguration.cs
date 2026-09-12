using FifthBox.ServerManager.App.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FifthBox.ServerManager.Storage.Configurations;

public class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        builder.ToTable("PlatformSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasMaxLength(32);
        builder.Property(s => s.RootDomain).HasMaxLength(253);
        builder.Property(s => s.AcmeEmail).HasMaxLength(320);
        builder.Property(s => s.ManagerPrefix).HasMaxLength(63);
        builder.Property(s => s.AcmeAccountDirectory).HasMaxLength(2048);
        builder.Property(s => s.AppliedProxyHash).HasMaxLength(64);
    }
}
