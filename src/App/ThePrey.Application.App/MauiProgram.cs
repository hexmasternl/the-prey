using Auth0.OidcClient;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

public static class MauiProgram
{
    internal const string Auth0Domain = "theprey.eu.auth0.com";
    internal const string Auth0ClientId = "tJrm2nPrAX4kES7XEnjUsL38cqbAbraJ";
    internal const string Auth0Audience = "https://api.theprey.eu";
    internal const string RedirectUri = "com.hexmaster.theprey.application.app://callback";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                // The Prey design system: Special Elite (display/headings) + PT Mono (body/data).
                fonts.AddFont("SpecialElite-Regular.ttf", "SpecialElite");
                fonts.AddFont("PTMono-Regular.ttf", "PTMono");
            });

        // Auth0Client is kept solely for RefreshTokenAsync (HTTP-only, no browser involved).
        builder.Services.AddSingleton(new Auth0Client(new Auth0ClientOptions
        {
            Domain = Auth0Domain,
            ClientId = Auth0ClientId,
            RedirectUri = RedirectUri,
            PostLogoutRedirectUri = RedirectUri,
            Scope = "openid profile email offline_access"
        }));

        builder.Services.AddSingleton<IAuthService, AuthService>();

        builder.Services.AddHttpClient("playfields", client =>
        {
            client.BaseAddress = new Uri("https://api.theprey.eu/");
        });
        builder.Services.AddHttpClient("games", client =>
        {
            client.BaseAddress = new Uri("https://api.theprey.eu/");
        });
        builder.Services.AddSingleton<IPlayfieldService, PlayfieldService>();
        builder.Services.AddSingleton<IGameService, GameService>();
        builder.Services.AddSingleton<GameStateContext>();
        builder.Services.AddSingleton<IGameEngineService, GameEngineService>();
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
