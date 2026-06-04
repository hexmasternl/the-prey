namespace ThePrey.Application.App;

public partial class AppShell : Shell
{
	public const string LoginRoute = "login";

	public const string PlayfieldsRoute = "playfields";
	public const string PlayfieldDetailsRoute = "playfield-details";
	public const string PlayfieldAreaEditorRoute = "playfield-area-editor";

	public const string GameStartRoute = "game-start";
	public const string GameLobbyRoute = "game-lobby";
	public const string GameProgressRoute = "game-progress";

	public AppShell()
	{
		InitializeComponent();

		// The login/welcome page is pushed over the main menu when the user is not authenticated.
		Routing.RegisterRoute(LoginRoute, typeof(LandingPage));
		Routing.RegisterRoute(PlayfieldsRoute, typeof(PlayfieldsPage));
		Routing.RegisterRoute(PlayfieldDetailsRoute, typeof(PlayfieldDetailsPage));
		Routing.RegisterRoute(PlayfieldAreaEditorRoute, typeof(PlayfieldAreaEditorPage));
		Routing.RegisterRoute(GameStartRoute, typeof(GameStartPage));
		Routing.RegisterRoute(GameLobbyRoute, typeof(GameLobbyPage));
		Routing.RegisterRoute(GameProgressRoute, typeof(GameProgressPage));
	}
}
