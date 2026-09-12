using FifthBox.Identity;
using FifthBox.Identity.AspNetCore;

namespace FifthBox.ServerManager.Host.Infrastructure;

/// <summary>
/// Wires up the app's auth. The dual cookie + JWT bearer scheme comes from
/// <c>FifthBox.Identity.AspNetCore</c>; this wrapper feeds it the config values (cookie name, JWT) and
/// registers the external providers we use. Google only shows up when it's configured.
/// </summary>
public static class AuthenticationSetup
{
    /// <summary>Temp cookie holding the external provider result during the OAuth callback.</summary>
    public const string ExternalScheme = IdentityAuthenticationExtensions.DefaultExternalScheme;

    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = new JwtOptions();
        configuration.GetSection("Identity:Jwt").Bind(jwt);

        var googleClientId = configuration["Authentication:Google:ClientId"];
        var googleClientSecret = configuration["Authentication:Google:ClientSecret"];

        return services.AddIdentityAuthentication(options =>
        {
            options.CookieName = "demoapp.auth";
            options.ExternalScheme = ExternalScheme;
            options.Jwt = jwt;

            // Escape hatch for a host without TLS yet. The session cookie travels in the clear when
            // this is on, so it belongs on a LAN box or a first deployment, never on anything public.
            options.AllowInsecureCookies = configuration.GetValue("Identity:AllowInsecureCookies", false);

            if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
            {
                options.ConfigureProviders = authentication => authentication.AddGoogle(google =>
                {
                    google.ClientId = googleClientId;
                    google.ClientSecret = googleClientSecret;
                    google.SignInScheme = ExternalScheme;
                });
            }
        });
    }
}
