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

        builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
        builder.Services.AddSingleton<ILocationProvider, GeolocationProvider>();
        builder.Services.AddSingleton<IAuthSession, AuthSession>();

        // handler and auth client have to share the same ITokenStore/IAuthSession singletons
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
