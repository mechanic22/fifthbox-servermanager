namespace FifthBox.ServerManager.Shared.Workloads;

public record SendConsoleRequest
{
    public string Text { get; init; } = string.Empty;
}
