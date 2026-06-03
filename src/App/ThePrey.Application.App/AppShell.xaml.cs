namespace ThePrey.Application.App;

public partial class AppShell : Shell
{
	public const string LoginRoute = "login";

	public const string PlayfieldsRoute = "playfields";
	public const string PlayfieldDetailsRoute = "playfield-details";
	public const string PlayfieldAreaEditorRoute = "playfield-area-editor";

	public AppShell()
	{
		InitializeComponent();

		// The login/welcome page is pushed over the main menu when the user is not authenticated.
		Routing.RegisterRoute(LoginRoute, typeof(LandingPage));
		Routing.RegisterRoute(PlayfieldsRoute, typeof(PlayfieldsPage));
		Routing.RegisterRoute(PlayfieldDetailsRoute, typeof(PlayfieldDetailsPage));
	}
}
