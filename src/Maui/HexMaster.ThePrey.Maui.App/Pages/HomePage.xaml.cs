using System.ComponentModel;
using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class HomePage : ContentPage
{
    // Handle used to commit/abort the player-name running-light animation.
    private const string PlayerNameSweep = "PlayerNameSweep";

    // Running-light cadence: a 1s sweep every 5s (so 4s idle between sweeps), per the design.
    private static readonly TimeSpan SweepDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SweepIdle = TimeSpan.FromSeconds(4);

    // Character spread of the soft green glow that travels across the name.
    private const double SweepTrail = 2.0;

    private readonly MainMenuViewModel _viewModel;
    private readonly List<Span> _playerNameSpans = new();

    // Base = dim/ghost text so the bright green running light really stands out against it; lit =
    // signal green. Resolved from the central Colors.xaml so the page carries no literals; the hex
    // fallbacks mirror TpTextGhost / TpSignal only if lookup fails.
    private readonly Color _playerNameBase;
    private readonly Color _playerNameLit;

    private CancellationTokenSource? _panCts;
    private CancellationTokenSource? _sweepCts;

    public HomePage(MainMenuViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _playerNameBase = ResolveColor("TpTextGhost", "#5a6553");
        _playerNameLit = ResolveColor("TpSignal", "#64ff00");

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartPan();
        BuildPlayerName(_viewModel.PlayerName);
        StartSweep();
        await _viewModel.LoadStateAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPan();
        StopSweep();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainMenuViewModel.PlayerName))
            Dispatcher.Dispatch(() => BuildPlayerName(_viewModel.PlayerName));
    }

    // Rebuilds the byline as one Span per character so the sweep can light them individually.
    private void BuildPlayerName(string? name)
    {
        _playerNameSpans.Clear();
        var formatted = new FormattedString();
        foreach (var ch in name ?? string.Empty)
        {
            var span = new Span { Text = ch.ToString(), TextColor = _playerNameBase };
            _playerNameSpans.Add(span);
            formatted.Spans.Add(span);
        }
        PlayerNameLabel.FormattedText = formatted;
    }

    private void StartPan()
    {
        _panCts?.Cancel();
        _panCts = new CancellationTokenSource();
        _ = PanLoopAsync(_panCts.Token);
    }

    private void StopPan()
    {
        _panCts?.Cancel();
        _panCts = null;
    }

    // Slow, continuous tactical drift across the map's overscan (the image is scaled >1 in its
    // style, so translating reveals map rather than empty edges). Stops when the page disappears.
    private async Task PanLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await MapBackground.TranslateToAsync(-40, -24, 12000, Easing.SinInOut);
                if (token.IsCancellationRequested) break;
                await MapBackground.TranslateToAsync(40, 24, 12000, Easing.SinInOut);
                if (token.IsCancellationRequested) break;
                await MapBackground.TranslateToAsync(0, 0, 12000, Easing.SinInOut);
            }
        }
        catch (Exception)
        {
            // Animation interrupted while the page is torn down — safe to ignore.
        }
    }

    private void StartSweep()
    {
        _sweepCts?.Cancel();
        _sweepCts = new CancellationTokenSource();
        _ = SweepLoopAsync(_sweepCts.Token);
    }

    private void StopSweep()
    {
        _sweepCts?.Cancel();
        _sweepCts = null;
        this.AbortAnimation(PlayerNameSweep);
    }

    // Runs a signal-green running light left-to-right across the player name once per period: a 1s
    // sweep followed by 4s of rest. Skips the sweep while the name has not resolved yet.
    private async Task SweepLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_playerNameSpans.Count > 0)
                    await RunSweepAsync(token);
                await Task.Delay(SweepIdle, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Page disappeared — stop looping.
        }
        catch (Exception)
        {
            // Animation interrupted during teardown — safe to ignore.
        }
    }

    // Animates a soft green glow whose head travels from before the first character to past the
    // last, lighting each character by its distance from the head, then restores the base color.
    private async Task RunSweepAsync(CancellationToken token)
    {
        var tcs = new TaskCompletionSource();

        void OnCancel()
        {
            this.AbortAnimation(PlayerNameSweep);
            tcs.TrySetResult();
        }

        using (token.Register(OnCancel))
        {
            var count = _playerNameSpans.Count;
            var animation = new Animation(progress =>
            {
                // Head sweeps from -trail to count+trail so the glow enters and exits cleanly.
                var head = progress * (count + 2 * SweepTrail) - SweepTrail;
                for (var i = 0; i < _playerNameSpans.Count; i++)
                {
                    var intensity = Math.Max(0, 1 - Math.Abs(i - head) / SweepTrail);
                    _playerNameSpans[i].TextColor = Lerp(_playerNameBase, _playerNameLit, intensity);
                }
            }, 0, 1, Easing.Linear);

            animation.Commit(this, PlayerNameSweep, length: (uint)SweepDuration.TotalMilliseconds,
                finished: (_, __) =>
                {
                    ResetPlayerNameColors();
                    tcs.TrySetResult();
                });

            await tcs.Task;
        }
    }

    private void ResetPlayerNameColors()
    {
        foreach (var span in _playerNameSpans)
            span.TextColor = _playerNameBase;
    }

    private static Color Lerp(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Color(
            (float)(from.Red + (to.Red - from.Red) * t),
            (float)(from.Green + (to.Green - from.Green) * t),
            (float)(from.Blue + (to.Blue - from.Blue) * t),
            (float)(from.Alpha + (to.Alpha - from.Alpha) * t));
    }

    private static Color ResolveColor(string key, string fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Color.FromArgb(fallback);
}
