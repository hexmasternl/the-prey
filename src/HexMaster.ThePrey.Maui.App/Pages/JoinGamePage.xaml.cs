using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class JoinGamePage : ContentPage, IQueryAttributable
{
    private readonly JoinGameViewModel _viewModel;

    public JoinGamePage(JoinGameViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Shell delivers the navigation query here before OnAppearing, so the pending game id is set on the view
    // model in time for the appear/gate logic. Keeping the IQueryAttributable plumbing on the page leaves the
    // view model free of MAUI types (fully unit-testable), mirroring GameLobbyPage.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(GameLobbyViewModel.GameIdQueryKey, out var value)
            && Guid.TryParse(value?.ToString(), out var gameId))
        {
            _viewModel.SetPendingGame(gameId);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}
