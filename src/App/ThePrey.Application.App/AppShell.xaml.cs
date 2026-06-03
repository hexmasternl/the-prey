namespace ThePrey.Application.App;

public partial class AppShell : Shell
{
	public const string LoginRoute = "login";

	public const string PlayfieldsRoute = "playfields";

	public AppShell()
	{
		InitializeComponent();

		// The login/welcome page is pushed over the main menu when the user is not authenticated.
		Routing.RegisterRoute(LoginRoute, typeof(LandingPage));
		Routing.RegisterRoute(PlayfieldsRoute, typeof(PlayfieldsPage));
	}
}
