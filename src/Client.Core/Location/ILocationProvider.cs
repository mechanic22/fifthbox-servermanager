namespace FifthBox.ServerManager.Client.Core;

public interface ILocationProvider
{
    /// null when permission's denied or there's no fix
    Task<DeviceLocation?> GetCurrentAsync(CancellationToken ct = default);
}

public sealed record DeviceLocation(double Latitude, double Longitude);
