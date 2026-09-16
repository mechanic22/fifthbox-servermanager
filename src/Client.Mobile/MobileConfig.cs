namespace FifthBox.ServerManager.Client.Mobile;

/// android emulators reach the host on 10.0.2.2, ios/maccatalyst use localhost
public static class MobileConfig
{
    public const string ApiBaseUrl = "http://localhost:5080/";
}
