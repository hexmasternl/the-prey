using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

/// <summary>A lobby player row: display name plus whether they are the designated hunter.</summary>
public sealed class GameLobbyItem
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool IsHunter { get; init; }
    public string HunterTag => AppLocalizer.HunterTag;
}

public partial class GameLobbyPage : ContentPage
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IGameService _gameService;
    private readonly GameCreationContext _creationContext;

    private Game? _game;
    private Guid _designatedHunterId;
    private CancellationTokenSource? _pollCts;
    private bool _isStarting;

    public GameLobbyPage(IGameService gameService, GameCreationContext creationContext)
    {
        InitializeComponent();
        _gameService = gameService;
        _creationContext = creationContext;

        Title = AppLocalizer.WaitingForPlayersTitle;
        GameCodeCaption.Text = AppLocalizer.GameCodeLabel;
        PlayersCaption.Text = AppLocalizer.PlayersLabel;
        StartButton.Text = AppLocalizer.StartNowButton;
    }

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _game = _creationContext.CurrentGame;
        if (_game is null)
            return;

        // The creator is the hunter until another player is designated by tapping.
        if (_designatedHunterId == Guid.Empty)
            _designatedHunterId = _game.OwnerUserId;

        GameCodeLabel.Text = _game.GameCode;
        RenderLobby();
        StartPolling();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPolling();
    }

    // ─── Polling ─────────────────────────────────────────────────────────────

    private void StartPolling()
    {
        StopPolling();
        var cts = new CancellationTokenSource();
        _pollCts = cts;
        _ = PollLoopAsync(cts.Token);
    }

    private void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, ct);

                if (_game is null || _isStarting)
                    continue;

                var refreshed = await _gameService.GetGameAsync(_game.Id, ct);
                if (refreshed is null)
                    continue;

                _game = refreshed;
                _creationContext.CurrentGame = refreshed;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!refreshed.IsInLobby)
                    {
                        // Started elsewhere — honor it the same as a local start.
                        await NavigateToProgressAsync();
                        return;
                    }

                    RenderLobby();
                });
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Transient refresh failure — keep polling.
            }
        }
    }

    // ─── Lobby rendering & hunter designation ───────────────────────────────

    private void RenderLobby()
    {
        if (_game is null)
            return;

        // Keep the designation valid: fall back to the creator when the designee left the lobby.
        if (_game.Lobby.All(p => p.UserId != _designatedHunterId))
            _designatedHunterId = _game.OwnerUserId;

        PlayersView.ItemsSource = _game.Lobby
            .Select(p => new GameLobbyItem
            {
                UserId = p.UserId,
                DisplayName = p.DisplayName,
                IsHunter = p.UserId == _designatedHunterId,
            })
            .ToList();

        UpdateStartButton();
    }

    private void OnPlayerTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not GameLobbyItem item)
            return;

        _designatedHunterId = item.UserId;
        RenderLobby();
    }

    private void UpdateStartButton() =>
        StartButton.IsEnabled = !_isStarting && _game is { } game && game.Lobby.Count >= 2;

    // ─── Start ───────────────────────────────────────────────────────────────

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        if (_game is null)
            return;

        _isStarting = true;
        UpdateStartButton();
        try
        {
            var started = await _gameService.StartGameAsync(_game.Id, _designatedHunterId);
            if (started is null)
            {
                await DisplayAlertAsync(AppLocalizer.Error, AppLocalizer.StartGameError, AppLocalizer.Ok);
                return;
            }

            _game = started;
            _creationContext.CurrentGame = started;
            await NavigateToProgressAsync();
        }
        catch (UnauthorizedException)
        {
            await Shell.Current.GoToAsync(AppShell.LoginRoute);
        }
        catch
        {
            // Stay in the lobby; polling is still active.
            await DisplayAlertAsync(AppLocalizer.Error, AppLocalizer.StartGameError, AppLocalizer.Ok);
        }
        finally
        {
            _isStarting = false;
            UpdateStartButton();
        }
    }

    /// <summary>Closes the create-game stack and shows Game Progress; back returns to the main menu.</summary>
    private async Task NavigateToProgressAsync()
    {
        StopPolling();
        await Shell.Current.GoToAsync($"../{AppShell.GameProgressRoute}");
    }
}
