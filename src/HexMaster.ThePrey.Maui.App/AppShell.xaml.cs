using HexMaster.ThePrey.Maui.App.Pages;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;

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
            // The `game` route now hosts the lobby; the started game hands off to the `gameplay` route,
            // which shows the placeholder GamePage until the separate gameplay change replaces it.
            Routing.RegisterRoute("game", typeof(GameLobbyPage));
            Routing.RegisterRoute(ShellLobbyNavigator.GameplayRoute, typeof(GamePage));
            // Role-specific gameplay pages the router hands off to. The prey + outcome routes point at the
            // placeholder GamePage until their own changes land (maui-game-play-page-prey / -outcome-page).
            Routing.RegisterRoute(GameplayRouter.HunterGameRoute, typeof(HunterGamePage));
            Routing.RegisterRoute(GameplayRouter.PreyGameRoute, typeof(GamePage));
            Routing.RegisterRoute(GameplayRouter.OutcomeRoute, typeof(GamePage));
            Routing.RegisterRoute("start-game", typeof(StartGamePage));
            // Invited-player entry point reached from the https://theprey.nl/join/{gameId} deep link.
            Routing.RegisterRoute(JoinGameViewModel.JoinRoute, typeof(JoinGamePage));
            Routing.RegisterRoute("playfields", typeof(PlayfieldsPage));
            Routing.RegisterRoute("settings", typeof(SettingsPage));
            Routing.RegisterRoute(ShellPlayfieldNavigator.CreatePlayfieldRoute, typeof(CreatePlayfieldPage));
            Routing.RegisterRoute(ShellPlayfieldNavigator.EditPlayfieldRoute, typeof(EditPlayfieldPage));
            Routing.RegisterRoute(ShellPlayfieldNavigator.DefineAreaRoute, typeof(DefineAreaPage));
            Routing.RegisterRoute(ShellPlayfieldSelectNavigator.SelectPlayfieldRoute, typeof(SelectPlayfieldPage));
        }
    }
}
