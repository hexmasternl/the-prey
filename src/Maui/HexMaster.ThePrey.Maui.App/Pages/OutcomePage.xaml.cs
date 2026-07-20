using System.Globalization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

/// <summary>
/// The full-screen post-game conclusion. Reads the finished game and the local player's role off the
/// route, has the view model resolve the outcome on appearing, and refuses the platform back gesture a
/// path into the dead game — back closes to the main menu, the same as the close button.
/// </summary>
public partial class OutcomePage : ContentPage, IQueryAttributable
{
    private readonly OutcomeViewModel _viewModel;

    public OutcomePage(OutcomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>Receives the <c>gameId</c> / <c>isHunter</c> the outcome navigator put on the route.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var gameId = Guid.TryParse(Value(query, ShellOutcomeNavigator.GameIdQueryKey), out var parsed)
            ? parsed
            : Guid.Empty;
        var isHunter = bool.TryParse(Value(query, ShellOutcomeNavigator.IsHunterQueryKey), out var hunter) && hunter;

        _viewModel.Initialize(gameId, isHunter);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        await PulseVictoryBadgeAsync();
    }

    /// <summary>
    /// The finished game is terminal: hardware back must not return to the frozen gameplay map. Route it
    /// through the same close path as the button, and report the press handled.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.CloseCommand.CanExecute(null))
            _viewModel.CloseCommand.Execute(null);
        return true;
    }

    // A single tasteful scale pulse on the victory badge. Purely additive: if the animation cannot run
    // (no result, a loss, or the platform declining it) the badge simply stays at its static size.
    private async Task PulseVictoryBadgeAsync()
    {
        if (!_viewModel.ShowsVictory)
            return;

        try
        {
            await VictoryBadge.ScaleTo(1.12, 220, Easing.CubicOut);
            await VictoryBadge.ScaleTo(1.0, 260, Easing.CubicIn);
        }
        catch (Exception)
        {
            // Cosmetic only — never let a failed animation surface to the player.
            VictoryBadge.Scale = 1.0;
        }
    }

    private static string? Value(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var raw)
            ? Convert.ToString(raw, CultureInfo.InvariantCulture)
            : null;
}
