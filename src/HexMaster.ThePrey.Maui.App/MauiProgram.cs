using System.Globalization;
using System.Reflection;
using System.Resources;
using HexMaster.ThePrey.Maui.App.Configuration;
using HexMaster.ThePrey.Maui.App.Pages;
using HexMaster.ThePrey.Maui.App.Services;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Platform;
using HexMaster.ThePrey.Maui.App.Services.Session;
using HexMaster.ThePrey.Maui.App.Services.Storage;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App
{
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
            services.AddSingleton(Preferences.Default);

            // Testable clock for debounced auto-save.
            services.AddSingleton(TimeProvider.System);

            // Session infrastructure.
            services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IAccessTokenProvider, AccessTokenProvider>();
            services.AddTransient<IInteractiveLoginService, InteractiveLoginService>();

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
            services.AddSingleton<IApplicationExit, ApplicationExit>();
            services.AddSingleton<IAppVersionProvider, MauiAppVersionProvider>();
            services.AddSingleton<IGpsReader, MauiGpsReader>();

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

            // View models.
            services.AddTransient<WelcomeViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainMenuViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<PlayFieldsListViewModel>();

            // Pages.
            services.AddTransient<WelcomePage>();
            services.AddTransient<LoginPage>();
            services.AddTransient<HomePage>();
            services.AddTransient<GamePage>();
            services.AddTransient<StartGamePage>();
            services.AddTransient<PlayfieldsPage>();
            services.AddTransient<SettingsPage>();
        }

        private static string EnsureTrailingSlash(string url) =>
            url.EndsWith('/') ? url : url + "/";
    }
}
