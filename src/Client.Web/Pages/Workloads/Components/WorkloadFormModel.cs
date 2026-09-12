using System.ComponentModel.DataAnnotations;
using FifthBox.ServerManager.Client.Web.Components;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

/// The editable state behind WorkloadConfigForm, shared by the create page and the detail Config tab.
/// Owns the mapping to/from the API DTOs.
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
    /// Whether to ask for an automatic address at create. Form-only, and create-only: afterwards the
    /// address is a route, and the route carries the port.
    public bool WebApp { get; set; }

    public string HttpPort { get; set; } = string.Empty;
    // 0 is "no limit", which the wire carries as null. Values come from ResourceScale, not free text.
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

    /// Blank for never; otherwise "HH:mm" local time.
    public string RestartDailyAt { get; set; } = string.Empty;
    public SourceKind SourceKind { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public int? SteamAppId { get; set; }
    public string SteamBranch { get; set; } = string.Empty;
    public string SteamUsername { get; set; } = string.Empty;

    /// Blank leaves the stored one alone, so editing anything else never round-trips the secret.
    public string SteamPassword { get; set; } = string.Empty;
    public bool HasSteamPassword { get; set; }

    public string HealthCommand { get; set; } = string.Empty;
    public int HealthIntervalSeconds { get; set; } = 10;
    public int HealthTimeoutSeconds { get; set; } = 3;
    public int HealthRetries { get; set; } = 3;
    public int HealthStartPeriodSeconds { get; set; } = 10;

    public List<KeyValueItem> Env { get; set; } = [];

    /// A row left at zero is dropped on the way out, so it needs saying — but as guidance, not an error,
    /// since that's how every row starts. These also drive the messages under the port editor.
    public bool HasBlankPortRow() => Ports.Any(p => p.Published == 0);

    public bool HasOutOfRangePortRow() => Ports.Any(p => p.Published != 0 && p.Published is < 1 or > 65535);

    public bool PortRowsInvalid() => HasBlankPortRow() || HasOutOfRangePortRow();

    /// Lowering a limit past its reservation would set aside more than the container is then allowed to
    /// use, which the server rejects — so the reservation comes down with it.
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

    /// Cheap value-equality for "has anything been edited?". Serialising the whole model keeps this
    /// honest when a field is added — a hand-written comparison would quietly stop covering it.
    public string Fingerprint() => System.Text.Json.JsonSerializer.Serialize(this);

    /// What's required depends on where it runs, which a per-property attribute can't see. Without
    /// this the omissions came back from the server as a snackbar on an eight-card form.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Target == WorkloadTarget.Swarm)
        {
            if (string.IsNullOrWhiteSpace(Image))
            {
                yield return new ValidationResult("An image is required, e.g. nginx:1.27.", [nameof(Image)]);
            }

            // Left unchecked these fail silently rather than loudly: a port that won't parse means "not a
            // web app", and a port row at zero is dropped on the way out.
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

    // A container publishes a port onto a container port, which may differ. A native process binds one
    // port, so the single number it was given serves as both. Always host-bound; the server re-derives
    // the mode anyway, so sending anything else is a lie.
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

    // 0 is the scale's "no limit" stop; the wire says that with null.
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

    /// Anything that isn't a readable time means "never" — the field is optional, and a half-typed
    /// "0" shouldn't schedule a restart for midnight.
    private int? RestartMinutes() =>
        TimeOnly.TryParse(RestartDailyAt, out var time) ? (time.Hour * 60) + time.Minute : null;
}
