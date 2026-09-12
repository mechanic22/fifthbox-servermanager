namespace FifthBox.ServerManager.Client.Mobile;

/// <summary>
/// Where the mobile app finds the Host. Android emulators reach the host loopback via 10.0.2.2;
/// iOS/MacCatalyst use localhost. The native (bearer) endpoints accept plain HTTP, so no cert trust
/// is needed on device for the demo.
/// </summary>
public static class MobileConfig
{
    public const string ApiBaseUrl = "http://localhost:5080/";
}
