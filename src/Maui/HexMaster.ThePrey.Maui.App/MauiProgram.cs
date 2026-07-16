using System.Globalization;
using System.Reflection;
using System.Resources;
using HexMaster.ThePrey.Maui.App.Configuration;
using HexMaster.ThePrey.Maui.App.Controls;
using HexMaster.ThePrey.Maui.App.Pages;
using HexMaster.ThePrey.Maui.App.Services;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Dialogs;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Platform;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using HexMaster.ThePrey.Maui.App.Services.Session;
using HexMaster.ThePrey.Maui.App.Services.Storage;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace HexMaster.ThePrey.Maui.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                // Mapsui renders through SkiaSharp; this call registers the SkiaSharp handlers the
                // area-editor MapControl needs (without it the control fails to render at runtime).
                .UseSkiaSharp()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                    // Tactical display face — the typewriter/stencil look used for headings and
                    // the wordmark (see TpDisplayFont in Styles.xaml).
                    fonts.AddFont("SpecialElite-Regular.ttf", "SpecialElite");
                    // Tactical body/readout monospace face (see TpBodyFont in Styles.xaml).
                    fonts.AddFont("PTMono-Regular.ttf", "PTMono");
                });

            LoadConfiguration(builder);
            RegisterServices(builder);

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        private static void LoadConfiguration(MauiAppBuilder builder)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("HexMaster.ThePrey.Maui.App.appsettings.json");
            if (stream is not null)
            {
                builder.Configuration.AddJsonStream(stream);
            }
        }

        private static void RegisterServices(MauiAppBuilder builder)
        {
            var services = builder.Services;

            services.Configure<ThePreyClientOptions>(
                builder.Configuration.GetSection(ThePreyClientOptions.SectionName));

            var options = new ThePreyClientOptions();
            builder.Configuration.GetSection(ThePreyClientOptions.SectionName).Bind(options);

            // Platform essentials.
            services.AddSingleton(SecureStorage.Default);
            services.AddSingleton(WebAuthenticator.Default);
            services.AddSingleton(Geolocation.Default);
            services.AddSingleton(Compass.Default);
            services.AddSingleton(Preferences.Default);

            // Testable clock for debounced auto-save.
            services.AddSingleton(TimeProvider.System);

            // Session infrastructure.
            services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IAccessTokenProvider, AccessTokenProvider>();
            services.AddTransient<IInteractiveLoginService, InteractiveLoginService>();
            services.AddTransient<IInteractiveLogoutService, InteractiveLogoutService>();

            // Local-first display cache of the private playfield list (a JSON file in the app data dir).
            services.AddSingleton<IPlayFieldCache>(sp => new PlayFieldCache(
                FileSystem.AppDataDirectory,
                sp.GetRequiredService<ILogger<PlayFieldCache>>()));

            // Localization — one runtime-switchable service over the embedded string resources,
            // a locally persisted preference, and a device-language-defaulting resolver.
            var resourceManager = new ResourceManager(
                "HexMaster.ThePrey.Maui.App.Resources.Strings.AppResources",
                typeof(MauiProgram).Assembly);
            services.AddSingleton<ILocalizationService>(new LocalizationService(resourceManager));
            services.AddSingleton<ILanguageStore, PreferencesLanguageStore>();
            services.AddSingleton<ILanguageResolver>(sp => new LanguageResolver(
                sp.GetRequiredService<ILanguageStore>(),
                () => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));

            // Menu-facing platform adapters (kept behind interfaces so view models stay testable).
            services.AddSingleton<IMenuNavigator, ShellMenuNavigator>();

            // Invite deep-link handler (parses https://theprey.nl/join/{gameId} → routes to the join page).
            // A singleton so a cold-start link queued by the platform survives until the Shell is ready.
            services.AddSingleton<IInviteDeepLinkHandler, InviteDeepLinkHandler>();

            // Confirm/cancel dialog seam (the delete flow's confirmation gate).
            services.AddSingleton<IConfirmationDialog, ConfirmationDialog>();

            // Create / area-editor navigation seam. One Shell-backed singleton carries the result
            // hand-off for both directions, exposed under both interfaces.
            services.AddSingleton<ShellPlayfieldNavigator>();
            services.AddSingleton<ICreatePlayfieldNavigator>(sp => sp.GetRequiredService<ShellPlayfieldNavigator>());
            services.AddSingleton<IEditPlayfieldNavigator>(sp => sp.GetRequiredService<ShellPlayfieldNavigator>());
            services.AddSingleton<IAreaEditorNavigator>(sp => sp.GetRequiredService<ShellPlayfieldNavigator>());

            // Playfield-selection modal navigator. One Shell-backed singleton is both the caller-facing
            // navigator and the modal view model's result sink.
            services.AddSingleton<ShellPlayfieldSelectNavigator>();
            services.AddSingleton<IPlayfieldSelectNavigator>(sp => sp.GetRequiredService<ShellPlayfieldSelectNavigator>());
            services.AddSingleton<IPlayfieldSelectResultSink>(sp => sp.GetRequiredService<ShellPlayfieldSelectNavigator>());
            services.AddSingleton<IApplicationExit, ApplicationExit>();
            services.AddSingleton<IAppVersionProvider, MauiAppVersionProvider>();
            services.AddSingleton<IGpsReader, MauiGpsReader>();

            // Continuous local position + compass-heading readers for the gameplay map's self marker.
            // Foreground-only, rendered locally (distinct from the background position-reporting capability).
            services.AddSingleton<ILivePositionReader, MauiLivePositionReader>();
            services.AddSingleton<IHeadingReader, MauiHeadingReader>();

            // Background location tracking: the platform-neutral coordinator + its public façade. The
            // coordinator owns the cadence loop, cadence adoption, retry, and start/stop rules; the two
            // native adapters below keep the process alive and supply fixes. One tracker per app session.
            services.AddSingleton<GameLocationTrackerCoordinator>();
            services.AddSingleton<IGameLocationTracker, GameLocationTracker>();
#if ANDROID
            services.AddSingleton<IBackgroundExecutionHost, AndroidBackgroundExecutionHost>();
            services.AddSingleton<IContinuousLocationSource, MauiGeolocationSource>();
#elif IOS
            // One CLLocationManager adapter is both the keep-alive host and the fix source.
            services.AddSingleton<IosBackgroundLocationManager>();
            services.AddSingleton<IBackgroundExecutionHost>(sp => sp.GetRequiredService<IosBackgroundLocationManager>());
            services.AddSingleton<IContinuousLocationSource>(sp => sp.GetRequiredService<IosBackgroundLocationManager>());
#else
            // Windows / MacCatalyst: no background execution — foreground-only reporting.
            services.AddSingleton<IBackgroundExecutionHost, NoopBackgroundExecutionHost>();
            services.AddSingleton<IContinuousLocationSource, MauiGeolocationSource>();
#endif

            // Auth0 token client (typed HttpClient).
            services.AddHttpClient<IAuth0TokenClient, Auth0TokenClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Backend game API client (typed HttpClient).
            services.AddHttpClient<IGameApiClient, GameApiClient>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(options.BackendBaseUrl));
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Backend users API client (typed HttpClient).
            services.AddHttpClient<IUserApiClient, UserApiClient>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(options.BackendBaseUrl));
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Backend playfields API client (typed HttpClient).
            services.AddHttpClient<IPlayFieldApiClient, PlayFieldApiClient>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(options.BackendBaseUrl));
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Background location-report client (typed HttpClient). POSTs each fix to
            // games/{id}/locations; a short timeout so a stalled report fails fast and retries next tick.
            services.AddHttpClient<ILocationReportClient, LocationReportClient>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(options.BackendBaseUrl));
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            // Lobby live-event stream (typed HttpClient). The SSE connection is long-lived, so it must
            // not carry a request timeout that would abort the stream — teardown is via cancellation.
            services.AddHttpClient<ILobbyStreamClient, LobbyStreamClient>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(options.BackendBaseUrl));
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

            // In-game real-time state. One shared singleton owns a single Web PubSub connection for the
            // active game and is the app's single source of truth for its state. The token flow reuses the
            // IGameApiClient typed HttpClient (a short GET); the long-lived transport is the ClientWebSocket
            // created by the factory below, not an HttpClient, so no infinite-timeout client is needed here.
            services.AddSingleton<IWebSocketConnectionFactory, NativeWebSocketConnectionFactory>();
            services.AddSingleton<IGameRealtimeConnection, GameRealtimeConnection>();
            services.AddSingleton<IGameStateService, GameStateService>();

            // Native share sheet.
            services.AddSingleton<IShareService, ShareService>();

            // Current-user id (from GET /users/me) — used to determine role at the gameplay hand-off.
            services.AddSingleton<ICurrentUserProvider, CurrentUserProvider>();

            // Gameplay router: fulfils the lobby's onward hand-off (ILobbyNavigator) by resolving the
            // active game's role and routing to the hunter/prey game page, and owns the outcome hand-off
            // (IGameplayNavigator). One Shell/navigator-backed singleton exposed under both interfaces.
            services.AddSingleton<GameplayRouter>();
            services.AddSingleton<ILobbyNavigator>(sp => sp.GetRequiredService<GameplayRouter>());
            services.AddSingleton<IGameplayNavigator>(sp => sp.GetRequiredService<GameplayRouter>());

            // In-game HUD seams: the hunter's tag-selection modal, and a placeholder map-camera signal
            // sink. The gameplay map change replaces NullMapCameraController with a real implementation
            // that moves the camera when the HUD's Center toggle emits follow/free-pan.
            services.AddSingleton<ITagDialog, TagDialog>();
            services.AddSingleton<IMapCameraController, NullMapCameraController>();

            // View models.
            services.AddTransient<WelcomeViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainMenuViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<PlayFieldsListViewModel>();
            services.AddTransient<CreatePlayfieldViewModel>();
            services.AddTransient<EditPlayfieldViewModel>();
            services.AddTransient<DefineAreaViewModel>();
            services.AddTransient<GameLobbyViewModel>();
            services.AddTransient<GameHudViewModel>();
            services.AddTransient<HunterGameViewModel>();
            services.AddTransient<PreyGameViewModel>();
            services.AddTransient<SelectPlayfieldViewModel>();
            services.AddTransient<StartGameViewModel>();
            services.AddTransient<JoinGameViewModel>();

            // Pages.
            services.AddTransient<WelcomePage>();
            services.AddTransient<LoginPage>();
            services.AddTransient<HomePage>();
            // GamePage remains registered as the placeholder gameplay screen (the `gameplay` route the
            // lobby hands off to); the `game` route itself now resolves GameLobbyPage.
            services.AddTransient<GamePage>();
            services.AddTransient<GameLobbyPage>();
            services.AddTransient<HunterGamePage>();
            services.AddTransient<PreyGamePage>();
            services.AddTransient<SelectPlayfieldPage>();
            services.AddTransient<StartGamePage>();
            services.AddTransient<JoinGamePage>();
            services.AddTransient<PlayfieldsPage>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<CreatePlayfieldPage>();
            services.AddTransient<EditPlayfieldPage>();
            services.AddTransient<DefineAreaPage>();

            // In-game HUD control. The (separate) gameplay map page resolves this and sets its
            // BindingContext to an initialized GameHudViewModel. TagCandidatesPage is constructed on
            // demand by TagDialog with its runtime candidate list, so it is not resolved from DI.
            services.AddTransient<GameHudView>();
        }

        private static string EnsureTrailingSlash(string url) =>
            url.EndsWith('/') ? url : url + "/";
    }
}
