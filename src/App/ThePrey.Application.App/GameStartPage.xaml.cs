using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

public partial class GameStartPage : ContentPage
{
    private static readonly int[] GameDurationChoices = [30, 60, 90];
    private static readonly int[] HunterDelayChoices = [5, 10, 15];
    private static readonly int[] FinalStageChoices = [5, 10, 15];
    private static readonly int[] DefaultIntervalChoices = [3, 5, 10];
    private static readonly int[] FinalIntervalChoices = [1, 2, 3];

    private readonly IGameService _gameService;
    private readonly IAuthService _authService;
    private readonly PlayfieldCacheService _playfieldCache;
    private readonly GameCreationContext _creationContext;

    private readonly OptionGroup _gameDuration;
    private readonly OptionGroup _hunterDelay;
    private readonly OptionGroup _finalStage;
    private readonly OptionGroup _defaultInterval;
    private readonly OptionGroup _finalInterval;

    private Playfield? _selectedPlayfield;
    private bool _isBusy;

    public GameStartPage(
        IGameService gameService,
        IAuthService authService,
        PlayfieldCacheService playfieldCache,
        GameCreationContext creationContext)
    {
        InitializeComponent();
        _gameService = gameService;
        _authService = authService;
        _playfieldCache = playfieldCache;
        _creationContext = creationContext;

        Title = AppLocalizer.GameStartTitle;
        PlayfieldLabel.Text = AppLocalizer.PlayfieldLabel;
        PlayfieldButton.Text = AppLocalizer.ChoosePlayfieldButton;
        GameDurationLabel.Text = AppLocalizer.GameDurationLabel;
        HunterDelayLabel.Text = AppLocalizer.HunterDelayLabel;
        FinalStageLabel.Text = AppLocalizer.FinalStageLabel;
        DefaultIntervalLabel.Text = AppLocalizer.DefaultIntervalLabel;
        FinalIntervalLabel.Text = AppLocalizer.FinalIntervalLabel;
        PreyPenaltyLabel.Text = AppLocalizer.PreyPenaltyLabel;
        HunterPenaltyLabel.Text = AppLocalizer.HunterPenaltyLabel;
        CreateButton.Text = AppLocalizer.CreateGameButton;

        // Fixed choices with the documented defaults; free-form entry is deliberately impossible.
        _gameDuration = new OptionGroup(GameDurationOptions, GameDurationChoices, defaultValue: 60);
        _hunterDelay = new OptionGroup(HunterDelayOptions, HunterDelayChoices, defaultValue: 10);
        _finalStage = new OptionGroup(FinalStageOptions, FinalStageChoices, defaultValue: 10);
        _defaultInterval = new OptionGroup(DefaultIntervalOptions, DefaultIntervalChoices, defaultValue: 5);
        _finalInterval = new OptionGroup(FinalIntervalOptions, FinalIntervalChoices, defaultValue: 2);

        UpdateCreateButton();
    }

    // ─── Playfield selection ─────────────────────────────────────────────────

    /// <summary>
    /// Lets the user pick the playfield for the game. Currently a chooser over the locally cached,
    /// synced playfields; integration point for the dedicated PlayfieldSelectPage when it lands.
    /// </summary>
    private async void OnChoosePlayfieldClicked(object? sender, EventArgs e)
    {
        var playfields = (await _playfieldCache.LoadAsync())
            .Where(p => p.IsSynchronized)
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (playfields.Count == 0)
        {
            await DisplayAlertAsync(AppLocalizer.GameStartTitle, AppLocalizer.NoPlayfieldsAvailable, AppLocalizer.Ok);
            return;
        }

        var choice = await DisplayActionSheetAsync(
            AppLocalizer.ChoosePlayfieldButton,
            AppLocalizer.Cancel,
            null,
            playfields.Select(p => p.Name).ToArray());

        var selected = playfields.FirstOrDefault(p => p.Name == choice);
        if (selected is null)
            return;

        _selectedPlayfield = selected;
        PlayfieldButton.Text = selected.Name;
        UpdateCreateButton();
    }

    // ─── Create ──────────────────────────────────────────────────────────────

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (_selectedPlayfield is null || !Guid.TryParse(_selectedPlayfield.Id, out var playfieldId))
            return;

        SetBusy(true);
        try
        {
            var game = await _gameService.CreateGameAsync(new CreateGameOptions
            {
                PlayfieldId = playfieldId,
                DisplayName = _authService.DisplayName ?? AppLocalizer.DefaultPlayerName,
                ProfilePictureUrl = _authService.ProfilePictureUrl,
                GameDurationMinutes = _gameDuration.SelectedValue,
                HunterDelayMinutes = _hunterDelay.SelectedValue,
                FinalStageMinutes = _finalStage.SelectedValue,
                DefaultLocationIntervalMinutes = _defaultInterval.SelectedValue,
                FinalLocationIntervalMinutes = _finalInterval.SelectedValue,
                EnablePreyBoundaryPenalty = PreyPenaltySwitch.IsToggled,
                EnableHunterBoundaryPenalty = HunterPenaltySwitch.IsToggled,
            });

            _creationContext.CurrentGame = game;

            // Replace this page with the lobby so back from the lobby skips the stale form.
            await Shell.Current.GoToAsync($"../{AppShell.GameLobbyRoute}");
        }
        catch (UnauthorizedException)
        {
            await Shell.Current.GoToAsync(AppShell.LoginRoute);
        }
        catch
        {
            // Selections are preserved; the user can retry.
            await DisplayAlertAsync(AppLocalizer.Error, AppLocalizer.CreateGameError, AppLocalizer.Ok);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        BusyIndicator.IsVisible = busy;
        BusyIndicator.IsRunning = busy;
        UpdateCreateButton();
    }

    private void UpdateCreateButton() =>
        CreateButton.IsEnabled = _selectedPlayfield is not null && !_isBusy;

    // ─── Fixed-choice option row ─────────────────────────────────────────────

    /// <summary>A row of mutually exclusive minute-value buttons; exactly one is selected.</summary>
    private sealed class OptionGroup
    {
        private readonly Dictionary<int, Button> _buttons = [];

        public int SelectedValue { get; private set; }

        public OptionGroup(Layout host, IReadOnlyList<int> choices, int defaultValue)
        {
            SelectedValue = defaultValue;

            foreach (var choice in choices)
            {
                var button = new Button
                {
                    Text = string.Format(AppLocalizer.MinutesFormat, choice),
                    FontFamily = "PTMono",
                    FontSize = 13,
                    CornerRadius = 3,
                    HeightRequest = 40,
                    Padding = new Thickness(14, 0),
                    BorderWidth = 1,
                };
                button.Clicked += (_, _) => Select(choice);
                _buttons[choice] = button;
                host.Children.Add(button);
            }

            ApplyVisuals();
        }

        private void Select(int value)
        {
            SelectedValue = value;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            foreach (var (value, button) in _buttons)
            {
                var selected = value == SelectedValue;
                button.BackgroundColor = selected ? Token("Signal") : Token("Surface");
                button.TextColor = selected ? Token("Void") : Token("TextSoft");
                button.BorderColor = selected ? Token("Signal") : Token("Line");
            }
        }

        private static Color Token(string key) =>
            (Color)Microsoft.Maui.Controls.Application.Current!.Resources[key];
    }
}
