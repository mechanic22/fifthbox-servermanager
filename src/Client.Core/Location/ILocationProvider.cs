namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// A device-location capability. Defined in Client.Core so view logic stays platform-neutral and
/// testable; the MAUI head implements it over <c>Geolocation</c>. Returns <c>null</c> when permission
/// is denied or a fix isn't available — callers decide how to surface that.
/// </summary>
public interface ILocationProvider
{
    Task<DeviceLocation?> GetCurrentAsync(CancellationToken ct = default);
}

/// <summary>A captured coordinate, ready to attach to a contact.</summary>
public sealed record DeviceLocation(double Latitude, double Longitude);
