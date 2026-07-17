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

            // Force dark mode app-wide, regardless of the operational OS theme. The app's own look is
            // a fixed tactical dark palette; this keeps the platform-drawn native popups (Android
            // Picker/alert dialogs, iOS pickers/UIAlertController) dark too. iOS/Mac reinforce this at
            // launch via UIUserInterfaceStyle=Dark in Info.plist.
            UserAppTheme = AppTheme.Dark;

            _deepLinkHandler = deepLinkHandler;

            // Apply the resolved language (persisted preference, else device language) before the
            // first page shows, and hand the service to the Translate markup extension.
            localization.SetLanguage(languageResolver.Resolve());
            TranslateExtension.Localization = localization;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // A cold-start invite link stays queued on the deep-link handler; the welcome bootstrap replays it
            // as its single post-boot navigation (see WelcomeViewModel), so nothing here races that decision.
            return new Window(new AppShell());
        }

        // A link received while the app is running: route to the Join Game page immediately.
        protected override void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);
            _ = _deepLinkHandler.TryHandleAsync(uri);
        }
    }
}
