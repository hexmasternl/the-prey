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
}
