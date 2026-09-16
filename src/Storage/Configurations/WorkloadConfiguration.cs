using System.Text.Json;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FifthBox.ServerManager.Storage.Configurations;

public class WorkloadConfiguration : IEntityTypeConfiguration<Workload>
{
    public void Configure(EntityTypeBuilder<Workload> builder)
    {
        builder.ToTable("Workloads");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasMaxLength(64);

        // it's the swarm service name, so unique app-wide
        builder.Property(w => w.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(w => w.Name).IsUnique();

        builder.Property(w => w.GroupId).HasMaxLength(64);
        builder.HasIndex(w => w.GroupId);

        builder.Property(w => w.Target).HasConversion<string>().HasMaxLength(16);
        builder.Property(w => w.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(w => w.AgentId).HasMaxLength(64);
        builder.HasIndex(w => w.AgentId);

        builder.Property(w => w.Image).HasMaxLength(500);
        builder.Property(w => w.Placement).HasConversion<string>().HasMaxLength(16)
            .HasDefaultValue(WorkloadPlacement.Auto);
        builder.Property(w => w.NodeId).HasMaxLength(64);

        builder.Property(w => w.Command).HasMaxLength(1000);
        builder.Property(w => w.WorkingDirectory).HasMaxLength(500);
        builder.Property(w => w.StopCommand).HasMaxLength(200);
        builder.Property(w => w.RestartDailyAtMinutes);
        builder.Property(w => w.ManagedDirectory).HasDefaultValue(false);


        // old rows predate these columns, defaults have to match what a new workload gets
        builder.Property(w => w.RestartPolicy).HasConversion<string>().HasMaxLength(16)
            .HasDefaultValue(RestartPolicy.OnFailure);
        builder.Property(w => w.StopGraceSeconds).HasDefaultValue(10);

        builder.Property(w => w.Env).HasConversion(JsonConverter<EnvVar>(), JsonComparer<EnvVar>());
        builder.Property(w => w.Ports).HasConversion(JsonConverter<PortMapping>(), JsonComparer<PortMapping>());
        builder.Property(w => w.Args).HasConversion(JsonConverter<string>(), JsonComparer<string>());
        builder.Property(w => w.Mounts).HasConversion(JsonConverter<VolumeMount>(), JsonComparer<VolumeMount>());
        builder.Property(w => w.Revisions).HasConversion(JsonConverter<WorkloadRevision>(), JsonComparer<WorkloadRevision>());
        builder.Property(w => w.Source).HasConversion(JsonObjectConverter<WorkloadSource>(), JsonObjectComparer<WorkloadSource>());
    }

    private static ValueConverter<List<T>, string> JsonConverter<T>() => new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => string.IsNullOrWhiteSpace(v)
            ? new List<T>()
            : JsonSerializer.Deserialize<List<T>>(v, (JsonSerializerOptions?)null) ?? new List<T>());

    // snapshot round-trips instead of ToList(), a shallow copy shares items so in-place edits never save
    // compares serialized too, revisions are mutable with no value equality
    private static ValueComparer<List<T>> JsonComparer<T>() => new(
        (a, b) => Serialized(a) == Serialized(b),
        v => Serialized(v).GetHashCode(StringComparison.Ordinal),
        v => JsonSerializer.Deserialize<List<T>>(Serialized(v), (JsonSerializerOptions?)null) ?? new List<T>());

    private static string Serialized<T>(List<T>? value)
        => JsonSerializer.Serialize(value ?? new List<T>(), (JsonSerializerOptions?)null);

    private static ValueConverter<T, string> JsonObjectConverter<T>() where T : class, new() => new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => string.IsNullOrWhiteSpace(v)
            ? new T()
            : JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null) ?? new T());

    private static ValueComparer<T> JsonObjectComparer<T>() where T : class, new() => new(
        (a, b) => SerializedObject(a) == SerializedObject(b),
        v => SerializedObject(v).GetHashCode(StringComparison.Ordinal),
        v => JsonSerializer.Deserialize<T>(SerializedObject(v), (JsonSerializerOptions?)null) ?? new T());

    private static string SerializedObject<T>(T? value) where T : class, new()
        => JsonSerializer.Serialize(value ?? new T(), (JsonSerializerOptions?)null);
}
