using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Client.Mobile.Pages;
using FifthBox.ServerManager.Client.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace FifthBox.ServerManager.Client.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Platform capabilities behind Client.Core interfaces — the app code never branches on platform.
        builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
        builder.Services.AddSingleton<ILocationProvider, GeolocationProvider>();
        builder.Services.AddSingleton<IAuthSession, AuthSession>();

        // Bearer pipeline: every API call carries the stored token and auto-refreshes on 401. The
        // typed clients get an HttpClient composed with BearerTokenHandler; the handler and the auth
        // client share the same ITokenStore / IAuthSession singletons.
        builder.Services.AddTransient<BearerTokenHandler>();
        builder.Services.AddHttpClient<IAuthClient, NativeAuthClient>(ConfigureApi).AddHttpMessageHandler<BearerTokenHandler>();
        builder.Services.AddHttpClient<IContactsClient, ContactsClient>(ConfigureApi).AddHttpMessageHandler<BearerTokenHandler>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ContactsListPage>();
        builder.Services.AddTransient<ContactCapturePage>();


        return builder.Build();
    }

    private static void ConfigureApi(HttpClient client)
        => client.BaseAddress = new Uri(MobileConfig.ApiBaseUrl);
}
