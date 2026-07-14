using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class HomePage : ContentPage
{
    private readonly MainMenuViewModel _viewModel;
    private CancellationTokenSource? _panCts;

    public HomePage(MainMenuViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartPan();
        await _viewModel.LoadStateAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPan();
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
}
