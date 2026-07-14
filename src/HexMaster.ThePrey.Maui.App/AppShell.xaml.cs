using HexMaster.ThePrey.Maui.App.Pages;

namespace HexMaster.ThePrey.Maui.App
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Non-shell-content destinations resolved via DI on navigation.
            Routing.RegisterRoute("login", typeof(LoginPage));
            Routing.RegisterRoute("home", typeof(HomePage));
            Routing.RegisterRoute("game", typeof(GamePage));
            Routing.RegisterRoute("start-game", typeof(StartGamePage));
            Routing.RegisterRoute("playfields", typeof(PlayfieldsPage));
            Routing.RegisterRoute("settings", typeof(SettingsPage));
        }
    }
}
