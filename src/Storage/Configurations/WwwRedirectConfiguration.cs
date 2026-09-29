using FifthBox.ServerManager.App.Routes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FifthBox.ServerManager.Storage.Configurations;

public class WwwRedirectConfiguration : IEntityTypeConfiguration<WwwRedirect>
{
    public void Configure(EntityTypeBuilder<WwwRedirect> builder)
    {
        builder.ToTable("WwwRedirects");
        builder.HasKey(w => w.Hostname);
        builder.Property(w => w.Hostname).HasMaxLength(253);
    }
}
