using Auth0.OidcClient;
using Microsoft.Extensions.Logging;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

public static class MauiProgram
{
    // TODO: Replace these placeholders with your actual Auth0 application credentials.
    // Domain:   Your Auth0 tenant domain, e.g. "your-tenant.auth0.com"
    // ClientId: The Client ID of your Auth0 native application.
    private const string Auth0Domain = "theprey.eu.auth0.com";
    private const string Auth0ClientId = "tJrm2nPrAX4kES7XEnjUsL38cqbAbraJ";

    // Audience (API identifier) requested at login so Auth0 issues a JWT access token that the
    // backend APIs accept. Must match the APIs' configured audience (Auth0:Audience).
    internal const string Auth0Audience = "https://api.theprey.eu";

    private const string RedirectUri = "com.hexmaster.theprey.application.app://callback";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                // The Prey design system: Special Elite (display/headings) + PT Mono (body/data).
                fonts.AddFont("SpecialElite-Regular.ttf", "SpecialElite");
                fonts.AddFont("PTMono-Regular.ttf", "PTMono");
            });

        builder.Services.AddSingleton(new Auth0Client(new Auth0ClientOptions
        {
            Domain = Auth0Domain,
            ClientId = Auth0ClientId,
            RedirectUri = RedirectUri,
            PostLogoutRedirectUri = RedirectUri,
            // offline_access requests a refresh token so the login session can be remembered/restored.
            Scope = "openid profile email offline_access"
        }));

        builder.Services.AddSingleton<IAuthService, AuthService>();

        builder.Services.AddHttpClient("playfields", client =>
        {
            client.BaseAddress = new Uri("https://api.theprey.eu/");
        });
        builder.Services.AddSingleton<IPlayfieldService, PlayfieldService>();
        builder.Services.AddSingleton<PlayfieldCacheService>();
        builder.Services.AddSingleton<PlayfieldSyncService>();
        builder.Services.AddTransient<PlayfieldsPage>();
        builder.Services.AddTransient<PlayfieldDetailsPage>();
        builder.Services.AddSingleton<PlayfieldEditingContext>();
        builder.Services.AddTransient<PlayfieldAreaEditorPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
