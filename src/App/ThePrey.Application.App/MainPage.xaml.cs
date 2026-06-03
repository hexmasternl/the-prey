using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

public partial class MainPage : ContentPage
{
    private bool _hasPromptedLogin;

    public MainPage()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    private static IAuthService? Auth => IPlatformApplication.Current?.Services.GetService<IAuthService>();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateMenuState();

        // The main menu is the first screen. On the first visit, route to the login/welcome page
        // so a remembered session can be restored or the operative can sign in. If they come back
        // without authenticating, we don't prompt again — the menu stays locked (except Quit).
        if (!(Auth?.IsAuthenticated ?? false) && !_hasPromptedLogin)
        {
            _hasPromptedLogin = true;
            try
            {
                await Shell.Current.GoToAsync(AppShell.LoginRoute);
            }
            catch
            {
                // If navigation isn't ready yet, allow a retry on the next appearance.
                _hasPromptedLogin = false;
            }
        }
    }

    private void ApplyLocalization()
    {
        TitleLabel.Text = AppLocalizer.AppTitle;
        PlayButton.Text = AppLocalizer.PlayButton;
        PlayfieldsButton.Text = AppLocalizer.PlayfieldsButton;
        FriendsButton.Text = AppLocalizer.FriendsButton;
        LogoutButton.Text = AppLocalizer.LogoutButton;
        QuitButton.Text = AppLocalizer.QuitButton;
    }

    private void UpdateMenuState()
    {
        var authenticated = Auth?.IsAuthenticated ?? false;

        // Play & Playfields require a logged-in operative; Friends is not available yet;
        // Logout only makes sense when authenticated; Quit is always allowed.
        PlayButton.IsEnabled = authenticated;
        PlayfieldsButton.IsEnabled = authenticated;
        FriendsButton.IsEnabled = false;
        LogoutButton.IsEnabled = authenticated;
        QuitButton.IsEnabled = true;
    }

    private async void OnPlayClicked(object? sender, EventArgs e)
        => await DisplayAlertAsync(AppLocalizer.PlayButton, "Starting a new hunt is coming soon.", "OK");

    private async void OnPlayfieldsClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppShell.PlayfieldsRoute);

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        var auth = Auth;
        if (auth is null) return;

        await auth.LogoutAsync();
        UpdateMenuState();
        await Shell.Current.GoToAsync(AppShell.LoginRoute);
    }

    private void OnQuitClicked(object? sender, EventArgs e)
        => Microsoft.Maui.Controls.Application.Current?.Quit();
}
