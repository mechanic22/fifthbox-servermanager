using System.ComponentModel.DataAnnotations;
using FifthBox.ServerManager.Shared.Routes;

namespace FifthBox.ServerManager.Client.Web.Components;

public sealed class RouteFormModel
{
    [Required(ErrorMessage = "Hostname is required.")]
    public string Hostname { get; set; } = string.Empty;

    public string Path { get; set; } = "/";

    /// set by whatever opened the form, never picked in it
    public RouteTarget Target { get; set; } = RouteTarget.Workload;

    public string WorkloadId { get; set; } = string.Empty;

    public string UpstreamHost { get; set; } = string.Empty;
    public UpstreamScheme UpstreamScheme { get; set; } = UpstreamScheme.Http;

    [Range(1, 65535, ErrorMessage = "Port must be 1-65535.")]
    public int TargetPort { get; set; } = 80;

    public bool WebSockets { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Max body size can't be negative. Use 0 for unlimited.")]
    public int? MaxBodySizeMb { get; set; }

    public bool BasicAuthEnabled { get; set; }
    public string BasicAuthUsername { get; set; } = string.Empty;
    /// blank on edit keeps the existing one
    public string BasicAuthPassword { get; set; } = string.Empty;

    public static RouteFormModel From(RouteResponse r) => new()
    {
        Hostname = r.Hostname,
        Path = r.Path,
        Target = r.Target,
        WorkloadId = r.WorkloadId ?? string.Empty,
        UpstreamHost = r.UpstreamHost ?? string.Empty,
        UpstreamScheme = r.UpstreamScheme,
        TargetPort = r.TargetPort,
        WebSockets = r.WebSockets,
        MaxBodySizeMb = r.MaxBodySizeMb,
        BasicAuthEnabled = r.BasicAuthEnabled,
        BasicAuthUsername = r.BasicAuthUsername ?? string.Empty,
    };

    public CreateRouteRequest ToCreateRequest() => new()
    {
        Hostname = Hostname,
        Path = string.IsNullOrWhiteSpace(Path) ? "/" : Path,
        Target = Target,
        WorkloadId = Target == RouteTarget.Workload ? WorkloadId : null,
        UpstreamHost = Target == RouteTarget.External ? UpstreamHost : null,
        UpstreamScheme = UpstreamScheme,
        TargetPort = TargetPort,
        WebSockets = WebSockets,
        MaxBodySizeMb = MaxBodySizeMb,
        BasicAuthEnabled = BasicAuthEnabled,
        BasicAuthUsername = BasicAuthEnabled ? BasicAuthUsername : null,
        BasicAuthPassword = string.IsNullOrEmpty(BasicAuthPassword) ? null : BasicAuthPassword,
    };

    public UpdateRouteRequest ToUpdateRequest() => new()
    {
        Hostname = Hostname,
        Path = string.IsNullOrWhiteSpace(Path) ? "/" : Path,
        Target = Target,
        WorkloadId = Target == RouteTarget.Workload ? WorkloadId : null,
        UpstreamHost = Target == RouteTarget.External ? UpstreamHost : null,
        UpstreamScheme = UpstreamScheme,
        TargetPort = TargetPort,
        WebSockets = WebSockets,
        MaxBodySizeMb = MaxBodySizeMb,
        BasicAuthEnabled = BasicAuthEnabled,
        BasicAuthUsername = BasicAuthEnabled ? BasicAuthUsername : null,
        BasicAuthPassword = string.IsNullOrEmpty(BasicAuthPassword) ? null : BasicAuthPassword,
    };
}
