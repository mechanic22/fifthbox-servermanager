using FifthBox.Identity;
using FifthBox.Identity.AspNetCore;

namespace FifthBox.ServerManager.Host.Infrastructure;

public static class AuthenticationSetup
{
    /// temp cookie holding the provider result during the oauth callback
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

            // for a box without tls yet. cookie goes in the clear, never turn on for anything public
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
