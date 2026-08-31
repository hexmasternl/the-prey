using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class GameLobbyPage : ContentPage, IQueryAttributable
{
    private readonly GameLobbyViewModel _viewModel;

    public GameLobbyPage(GameLobbyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Shell delivers the navigation query here before OnAppearing, so the target game id is set on the
    // view model in time for ActivateAsync to load that game by id (rather than resolving the active game).
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(GameLobbyViewModel.GameIdQueryKey, out var value)
            && Guid.TryParse(value?.ToString(), out var gameId))
        {
            _viewModel.SetTargetGame(gameId);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.ActivateAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Deactivate();
    }

    /// <summary>
    /// Backing out of the lobby means leaving the game, so it goes through the server before the screen
    /// closes — otherwise the server keeps this player in the lobby while they walk away. The default pop
    /// is suppressed so the withdraw can complete first; we then navigate ourselves.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        _ = LeaveAndNavigateAsync();
        return true;
    }

    private async Task LeaveAndNavigateAsync()
    {
        // false means the game has already started: there is nothing to withdraw from and the lobby is
        // about to hand off to the gameplay screen, so leaving here would fight that navigation.
        if (!await _viewModel.LeaveAsync())
            return;

        // Shell navigation must happen on the UI thread; dispatch explicitly rather than relying on the
        // continuation resuming there.
        Dispatcher.Dispatch(() => _ = Shell.Current.GoToAsync(".."));
    }
}
