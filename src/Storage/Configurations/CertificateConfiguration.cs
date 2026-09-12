using FifthBox.ServerManager.App.Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FifthBox.ServerManager.Storage.Configurations;

public class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("Certificates");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasMaxLength(32);
        builder.Property(c => c.Hostname).HasMaxLength(253).IsRequired();
        builder.HasIndex(c => c.Hostname).IsUnique();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.LastError).HasMaxLength(2048);
    }
}
