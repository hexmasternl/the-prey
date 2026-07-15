using HexMaster.ThePrey.Maui.App.Services.Localization;

namespace HexMaster.ThePrey.Maui.App
{
    public partial class App : Application
    {
        public App(ILocalizationService localization, ILanguageResolver languageResolver)
        {
            InitializeComponent();

            // Apply the resolved language (persisted preference, else device language) before the
            // first page shows, and hand the service to the Translate markup extension.
            localization.SetLanguage(languageResolver.Resolve());
            TranslateExtension.Localization = localization;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}
