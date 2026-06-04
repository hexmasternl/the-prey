using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

/// <summary>
/// Placeholder for the in-game view shown once a game has started. The real game-progress UI
/// (map, countdowns, hunter/prey state fed by the background game engine) is a future change.
/// </summary>
public partial class GameProgressPage : ContentPage
{
    private readonly GameCreationContext _creationContext;

    public GameProgressPage(GameCreationContext creationContext)
    {
        InitializeComponent();
        _creationContext = creationContext;

        Title = AppLocalizer.GameProgressTitle;
        GameCodeCaption.Text = AppLocalizer.GameCodeLabel;
        PlaceholderLabel.Text = AppLocalizer.GameProgressPlaceholder;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        GameCodeLabel.Text = _creationContext.CurrentGame?.GameCode ?? string.Empty;
    }
}
