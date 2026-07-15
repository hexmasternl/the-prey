using HexMaster.ThePrey.Maui.App.Pages;
using HexMaster.ThePrey.Maui.App.Services.Navigation;

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
            Routing.RegisterRoute(ShellPlayfieldNavigator.CreatePlayfieldRoute, typeof(CreatePlayfieldPage));
            Routing.RegisterRoute(ShellPlayfieldNavigator.EditPlayfieldRoute, typeof(EditPlayfieldPage));
            Routing.RegisterRoute(ShellPlayfieldNavigator.DefineAreaRoute, typeof(DefineAreaPage));
        }
    }
}
