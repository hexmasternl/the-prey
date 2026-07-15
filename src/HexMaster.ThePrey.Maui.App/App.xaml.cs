using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;

namespace HexMaster.ThePrey.Maui.App
{
    public partial class App : Application
    {
        private readonly IInviteDeepLinkHandler _deepLinkHandler;

        public App(
            ILocalizationService localization,
            ILanguageResolver languageResolver,
            IInviteDeepLinkHandler deepLinkHandler)
        {
            InitializeComponent();

            _deepLinkHandler = deepLinkHandler;

            // Apply the resolved language (persisted preference, else device language) before the
            // first page shows, and hand the service to the Translate markup extension.
            localization.SetLanguage(languageResolver.Resolve());
            TranslateExtension.Localization = localization;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            // Replay any invite link captured from a cold-start launch once the window (and Shell) are up.
            window.Activated += async (_, _) => await _deepLinkHandler.ReplayPendingAsync();

            return window;
        }

        // A link received while the app is running: route to the Join Game page immediately.
        protected override void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);
            _ = _deepLinkHandler.TryHandleAsync(uri);
        }
    }
}
