using Auth0.OidcClient;
using Microsoft.Extensions.Logging;

namespace ThePrey.Application.App;

public static class MauiProgram
{
    // TODO: Replace these placeholders with your actual Auth0 application credentials.
    // Domain:   Your Auth0 tenant domain, e.g. "your-tenant.auth0.com"
    // ClientId: The Client ID of your Auth0 native application.
    private const string Auth0Domain = "YOUR_AUTH0_DOMAIN";
    private const string Auth0ClientId = "YOUR_AUTH0_CLIENT_ID";

    private const string RedirectUri = "com.companyname.theprey.application.app://callback";

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

        builder.Services.AddSingleton(new Auth0Client(new Auth0ClientOptions
        {
            Domain = Auth0Domain,
            ClientId = Auth0ClientId,
            RedirectUri = RedirectUri,
            PostLogoutRedirectUri = RedirectUri,
            Scope = "openid profile email"
        }));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
