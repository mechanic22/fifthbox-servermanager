using System.Text.Json;
using FifthBox.ServerManager.App.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FifthBox.ServerManager.Storage.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasMaxLength(64);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.HasIndex(t => t.Name).IsUnique();

        builder.Property(t => t.MemberIds).HasConversion(JsonConverter(), JsonComparer());
    }

    private static ValueConverter<List<string>, string> JsonConverter() => new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => string.IsNullOrWhiteSpace(v)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

    private static ValueComparer<List<string>> JsonComparer() => new(
        (a, b) => Serialized(a) == Serialized(b),
        v => Serialized(v).GetHashCode(StringComparison.Ordinal),
        v => JsonSerializer.Deserialize<List<string>>(Serialized(v), (JsonSerializerOptions?)null) ?? new List<string>());

    private static string Serialized(List<string>? value)
        => JsonSerializer.Serialize(value ?? new List<string>(), (JsonSerializerOptions?)null);
}
