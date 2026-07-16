using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class JoinGamePage : ContentPage, IQueryAttributable
{
    // Laser cadence: a single top→bottom sweep every 10s (a ~1.8s sweep, then idle for the remainder).
    private static readonly TimeSpan LaserSweepDuration = TimeSpan.FromSeconds(1.8);
    private static readonly TimeSpan LaserPeriod = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LaserIdle = LaserPeriod - LaserSweepDuration;

    // Must match the JoinLaserSweep style's HeightRequest — the band is parked this far above the top
    // between sweeps so it is fully off-screen.
    private const double LaserBandHeight = 60;

    private readonly JoinGameViewModel _viewModel;

    private CancellationTokenSource? _panCts;
    private CancellationTokenSource? _laserCts;

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
        StartPan();
        StartLaser();
        await _viewModel.OnAppearingAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPan();
        StopLaser();
    }

    // --- Panning tactical map background (mirrors the main menu's drift) ---

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

    // Slow, continuous tactical drift across the map's overscan (the image is scaled >1 in its style,
    // so translating reveals map rather than empty edges). Stops when the page disappears.
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

    // --- Green laser scan sweep (top→bottom, once every 10s) ---

    private void StartLaser()
    {
        _laserCts?.Cancel();
        _laserCts = new CancellationTokenSource();
        _ = LaserLoopAsync(_laserCts.Token);
    }

    private void StopLaser()
    {
        _laserCts?.Cancel();
        _laserCts = null;
    }

    private async Task LaserLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                // Wait the full period when the page has not laid out yet (sweep skipped), otherwise
                // just the remainder after the sweep — so a sweep starts once per 10s.
                var swept = await RunLaserSweepAsync(token);
                await Task.Delay(swept ? LaserIdle : LaserPeriod, token);
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

    // Drives the beam from just above the top to just past the bottom, then parks it off-screen. Returns
    // false (so the caller waits a full period and retries) when the page has no measured height yet.
    private async Task<bool> RunLaserSweepAsync(CancellationToken token)
    {
        var travel = RootGrid.Height;
        if (travel <= 0)
            return false;

        LaserSweep.TranslationY = -LaserBandHeight;
        LaserSweep.Opacity = 1;
        await LaserSweep.TranslateToAsync(0, travel, (uint)LaserSweepDuration.TotalMilliseconds, Easing.Linear);

        LaserSweep.Opacity = 0;
        LaserSweep.TranslationY = -LaserBandHeight; // park above the top until the next sweep
        return true;
    }
}
