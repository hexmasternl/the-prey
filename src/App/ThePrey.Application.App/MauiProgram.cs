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

    internal const string DefaultBackendUrl = "https://api.theprey.eu/";

    /// <summary>
    /// Backend host base URL. Defaults to <see cref="DefaultBackendUrl"/> and can be
    /// overridden with the BACKEND_URL environment variable (set by Aspire to the
    /// YARP gateway endpoint during local development).
    /// </summary>
    internal static Uri BackendUrl { get; } = ResolveBackendUrl();

    private static Uri ResolveBackendUrl()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("BACKEND_URL");
        if (string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return new Uri(DefaultBackendUrl);
        }

        // HttpClient.BaseAddress needs a trailing slash for relative paths to resolve correctly.
        return new Uri(fromEnvironment.EndsWith('/') ? fromEnvironment : fromEnvironment + "/");
    }

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

        // The whole OAuth flow (PKCE login, code exchange, refresh, revoke) is raw HTTP inside
        // AuthService; the Auth0.OidcClient library is no longer used at runtime.
        builder.Services.AddSingleton<IAuthService, AuthService>();

        builder.Services.AddHttpClient("playfields", client =>
        {
            client.BaseAddress = BackendUrl;
        });
        builder.Services.AddHttpClient("games", client =>
        {
            client.BaseAddress = BackendUrl;
        });
        builder.Services.AddSingleton<IPlayfieldService, PlayfieldService>();
        builder.Services.AddSingleton<IGameService, GameService>();
        builder.Services.AddSingleton<GameStateContext>();
        builder.Services.AddSingleton<GameCreationContext>();
        builder.Services.AddSingleton<IGameEngineService, GameEngineService>();
        builder.Services.AddSingleton<PlayfieldCacheService>();
        builder.Services.AddSingleton<PlayfieldSyncService>();
        builder.Services.AddTransient<PlayfieldsPage>();
        builder.Services.AddTransient<PlayfieldDetailsPage>();
        builder.Services.AddSingleton<PlayfieldEditingContext>();
        builder.Services.AddTransient<PlayfieldAreaEditorPage>();
        builder.Services.AddTransient<GameStartPage>();
        builder.Services.AddTransient<GameLobbyPage>();
        builder.Services.AddTransient<GameProgressPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
