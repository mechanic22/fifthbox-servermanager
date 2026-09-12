namespace FifthBox.ServerManager.Shared.Platform;

public record UpdatePlatformSettingsRequest
{
    public string? RootDomain { get; init; }
    public string? AcmeEmail { get; init; }
    public string? ManagerPrefix { get; init; }
}
