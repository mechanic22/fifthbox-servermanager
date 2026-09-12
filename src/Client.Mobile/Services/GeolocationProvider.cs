using FifthBox.ServerManager.Client.Core;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace FifthBox.ServerManager.Client.Mobile.Services;

/// <summary>
/// <see cref="ILocationProvider"/> over MAUI <see cref="Geolocation"/>. Thin adapter: request
/// permission, ask for a medium-accuracy fix, map to the Core <see cref="DeviceLocation"/>. Returns
/// <c>null</c> when permission is denied or no fix is available.
/// </summary>
public sealed class GeolocationProvider : ILocationProvider
{
    public async Task<DeviceLocation?> GetCurrentAsync(CancellationToken ct = default)
    {
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            return null;
        }

        var location = await Geolocation.Default.GetLocationAsync(
            new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)), ct);

        return location is null ? null : new DeviceLocation(location.Latitude, location.Longitude);
    }
}
