using System.ComponentModel.DataAnnotations;
using FifthBox.ServerManager.Client.Web.Components;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

public sealed class WorkloadFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Name is required.")]
    public string Name { get; set; } = string.Empty; // create only

    public string GroupId { get; set; } = string.Empty;
    public WorkloadTarget Target { get; set; } = WorkloadTarget.Swarm;
    public string AgentId { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;
    public WorkloadMode Mode { get; set; }
    public int Replicas { get; set; } = 1;
    public WorkloadPlacement Placement { get; set; } = WorkloadPlacement.Auto;
    public string NodeId { get; set; } = string.Empty;
    public List<PortRow> Ports { get; set; } = [];
    /// create only, afterwards the address is a route
    public bool WebApp { get; set; }

    public string HttpPort { get; set; } = string.Empty;
    // 0 is "no limit", sent as null
    public int MemoryLimitMb { get; set; }
    public double CpuLimit { get; set; }
    public int MemoryReserveMb { get; set; }
    public double CpuReserve { get; set; }
    public List<VolumeRow> Mounts { get; set; } = [];

    public string Command { get; set; } = string.Empty;
    public string ArgsText { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.OnFailure;
    public int StopGraceSeconds { get; set; } = 10;
    public string StopCommand { get; set; } = string.Empty;
    public bool ManagedDirectory { get; set; }

    /// blank for never, else "HH:mm" local
    public string RestartDailyAt { get; set; } = string.Empty;
    public SourceKind SourceKind { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public int? SteamAppId { get; set; }
    public string SteamBranch { get; set; } = string.Empty;
    public string SteamUsername { get; set; } = string.Empty;

    /// blank keeps the stored one
    public string SteamPassword { get; set; } = string.Empty;
    public bool HasSteamPassword { get; set; }

    public string HealthCommand { get; set; } = string.Empty;
    public int HealthIntervalSeconds { get; set; } = 10;
    public int HealthTimeoutSeconds { get; set; } = 3;
    public int HealthRetries { get; set; } = 3;
    public int HealthStartPeriodSeconds { get; set; } = 10;

    public List<KeyValueItem> Env { get; set; } = [];

    /// zero rows get dropped on save, shown as guidance not an error
    public bool HasBlankPortRow() => Ports.Any(p => p.Published == 0);

    public bool HasOutOfRangePortRow() => Ports.Any(p => p.Published != 0 && p.Published is < 1 or > 65535);

    public bool PortRowsInvalid() => HasBlankPortRow() || HasOutOfRangePortRow();

    /// drags the reservation down too, the server rejects one above the limit
    public void SetMemoryLimit(int mb)
    {
        MemoryLimitMb = mb;
        if (mb > 0 && MemoryReserveMb > mb)
        {
            MemoryReserveMb = mb;
        }
    }

    public void SetCpuLimit(double cores)
    {
        CpuLimit = cores;
        if (cores > 0 && CpuReserve > cores)
        {
            CpuReserve = cores;
        }
    }

    /// serialises the whole model so new fields are covered automatically
    public string Fingerprint() => System.Text.Json.JsonSerializer.Serialize(this);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Target == WorkloadTarget.Swarm)
        {
            if (string.IsNullOrWhiteSpace(Image))
            {
                yield return new ValidationResult("An image is required, e.g. nginx:1.27.", [nameof(Image)]);
            }

            // these fail silently otherwise: a bad port means "not a web app", a zero row gets dropped
            if (WebApp && !IsPort(HttpPort))
            {
                yield return new ValidationResult(
                    "Enter the port your app listens on inside the container, e.g. 8080.", [nameof(HttpPort)]);
            }

            if (Placement == WorkloadPlacement.Node && string.IsNullOrWhiteSpace(NodeId))
            {
                yield return new ValidationResult("Pick the node this runs on.", [nameof(NodeId)]);
            }

            if (PortRowsInvalid())
            {
                yield return new ValidationResult(
                    "Every port row needs a published port between 1 and 65535.", [nameof(Ports)]);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(AgentId))
            {
                yield return new ValidationResult("Choose the agent this runs on.", [nameof(AgentId)]);
            }

            if (string.IsNullOrWhiteSpace(Command))
            {
                yield return new ValidationResult("A command is required.", [nameof(Command)]);
            }
        }
    }

    public static WorkloadFormModel From(WorkloadResponse w) => new()
    {
        Name = w.Name,
        GroupId = w.GroupId ?? string.Empty,
        Target = w.Target,
        AgentId = w.AgentId ?? string.Empty,
        Image = w.Image ?? string.Empty,
        Mode = w.Mode,
        Replicas = w.Replicas,
        Placement = w.Placement,
        NodeId = w.NodeId ?? string.Empty,
        Ports = w.Ports.Select(p => new PortRow { Published = p.Published, Target = p.Target, Protocol = p.Protocol }).ToList(),
        MemoryLimitMb = w.MemoryLimitMb ?? 0,
        CpuLimit = w.CpuLimit ?? 0,
        MemoryReserveMb = w.MemoryReserveMb ?? 0,
        CpuReserve = w.CpuReserve ?? 0,
        Mounts = w.Mounts.Select(m => new VolumeRow { Type = m.Type, Source = m.Source, Target = m.Target, ReadOnly = m.ReadOnly }).ToList(),
        Command = w.Command ?? string.Empty,
        ArgsText = string.Join('\n', w.Args),
        WorkingDirectory = w.WorkingDirectory ?? string.Empty,
        RestartPolicy = w.RestartPolicy,
        StopGraceSeconds = w.StopGraceSeconds,
        StopCommand = w.StopCommand ?? string.Empty,
        ManagedDirectory = w.ManagedDirectory,
        RestartDailyAt = w.RestartDailyAtMinutes is { } m ? $"{m / 60:00}:{m % 60:00}" : string.Empty,
        SourceKind = w.Source?.Kind ?? SourceKind.None,
        SourceUrl = w.Source?.Url ?? string.Empty,
        SteamAppId = w.Source?.SteamAppId,
        SteamBranch = w.Source?.SteamBranch ?? string.Empty,
        SteamUsername = w.Source?.SteamUsername ?? string.Empty,
        HasSteamPassword = w.Source?.HasSteamPassword ?? false,
        HealthCommand = w.HealthCommand ?? string.Empty,
        HealthIntervalSeconds = w.HealthIntervalSeconds,
        HealthTimeoutSeconds = w.HealthTimeoutSeconds,
        HealthRetries = w.HealthRetries,
        HealthStartPeriodSeconds = w.HealthStartPeriodSeconds,
        Env = w.Env.Select(e => new KeyValueItem { Key = e.Key, Value = e.Value, Secret = e.Secret }).ToList(),
    };

    public CreateWorkloadRequest ToCreateRequest() => new()
    {
        Name = Name,
        GroupId = string.IsNullOrEmpty(GroupId) ? null : GroupId,
        Target = Target,
        AgentId = Target == WorkloadTarget.Agent ? AgentId : null,
        Image = Image,
        Mode = Mode,
        Replicas = Replicas,
        Placement = Placement,
        NodeId = string.IsNullOrEmpty(NodeId) ? null : NodeId,
        Ports = PortMappings(),
        HttpPort = WebApp ? ParsePort(HttpPort) : null,
        MemoryLimitMb = Unset(MemoryLimitMb),
        CpuLimit = Unset(CpuLimit),
        MemoryReserveMb = Unset(MemoryReserveMb),
        CpuReserve = Unset(CpuReserve),
        Mounts = VolumeMounts(),
        Command = Command,
        Args = ArgList(),
        WorkingDirectory = WorkingDirectory,
        RestartPolicy = RestartPolicy,
        StopGraceSeconds = StopGraceSeconds,
        StopCommand = StopCommand,
        ManagedDirectory = ManagedDirectory,
        Source = SourceRequest(),
        RestartDailyAtMinutes = RestartMinutes(),
        HealthCommand = HealthCommand,
        HealthIntervalSeconds = HealthIntervalSeconds,
        HealthTimeoutSeconds = HealthTimeoutSeconds,
        HealthRetries = HealthRetries,
        HealthStartPeriodSeconds = HealthStartPeriodSeconds,
        Env = EnvVars(),
    };

    public UpdateWorkloadRequest ToUpdateRequest() => new()
    {
        GroupId = string.IsNullOrEmpty(GroupId) ? null : GroupId,
        Image = Image,
        Mode = Mode,
        Replicas = Replicas,
        Placement = Placement,
        NodeId = string.IsNullOrEmpty(NodeId) ? null : NodeId,
        Ports = PortMappings(),
        MemoryLimitMb = Unset(MemoryLimitMb),
        CpuLimit = Unset(CpuLimit),
        MemoryReserveMb = Unset(MemoryReserveMb),
        CpuReserve = Unset(CpuReserve),
        Mounts = VolumeMounts(),
        Command = Command,
        Args = ArgList(),
        WorkingDirectory = WorkingDirectory,
        RestartPolicy = RestartPolicy,
        StopGraceSeconds = StopGraceSeconds,
        StopCommand = StopCommand,
        ManagedDirectory = ManagedDirectory,
        Source = SourceRequest(),
        RestartDailyAtMinutes = RestartMinutes(),
        HealthCommand = HealthCommand,
        HealthIntervalSeconds = HealthIntervalSeconds,
        HealthTimeoutSeconds = HealthTimeoutSeconds,
        HealthRetries = HealthRetries,
        HealthStartPeriodSeconds = HealthStartPeriodSeconds,
        Env = EnvVars(),
    };

    // native binds one port, so it's both published and target
    // always host mode, the server re-derives it anyway
    private List<PortMapping> PortMappings() =>
        Ports
            .Where(p => p.Published > 0)
            .Select(p => new PortMapping(
                p.Published,
                Target == WorkloadTarget.Agent || p.Target <= 0 ? p.Published : p.Target,
                p.Protocol,
                PortPublishMode.Host))
            .ToList();

    private List<VolumeMount> VolumeMounts() => Mounts
        .Where(m => !string.IsNullOrWhiteSpace(m.Source) && !string.IsNullOrWhiteSpace(m.Target))
        .Select(m => new VolumeMount(m.Type, m.Source.Trim(), m.Target.Trim(), m.ReadOnly))
        .ToList();

    private List<EnvVar> EnvVars() => Env
        .Where(e => e.Key.Length > 0)
        .Select(e => new EnvVar(e.Key, e.Value, e.Secret))
        .ToList();

    private List<string> ArgList() =>
        ArgsText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static int? ParsePort(string value) => int.TryParse(value, out var p) ? p : null;

    private static bool IsPort(string value) => int.TryParse(value, out var p) && p is >= 1 and <= 65535;

    private static int? Unset(int value) => value <= 0 ? null : value;

    private static double? Unset(double value) => value <= 0 ? null : value;

    private WorkloadSourceRequest? SourceRequest() => SourceKind == SourceKind.None ? null : new WorkloadSourceRequest
    {
        Kind = SourceKind,
        Url = SourceUrl,
        SteamAppId = SteamAppId,
        SteamBranch = SteamBranch,
        SteamUsername = SteamUsername,
        SteamPassword = SteamPassword,
    };

    /// anything unparseable is "never", so a half-typed "0" doesn't schedule midnight
    private int? RestartMinutes() =>
        TimeOnly.TryParse(RestartDailyAt, out var time) ? (time.Hour * 60) + time.Minute : null;
}
