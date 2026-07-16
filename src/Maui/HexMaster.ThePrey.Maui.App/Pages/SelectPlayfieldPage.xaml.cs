using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class SelectPlayfieldPage : ContentPage
{
    private readonly SelectPlayfieldViewModel _viewModel;
    private bool _loaded;

    public SelectPlayfieldPage(SelectPlayfieldViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;

        _loaded = true;
        await _viewModel.LoadDefaultAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Catch-all for a system-back / swipe dismiss: resolve the caller's awaited result with nothing.
        // Idempotent — if confirm/cancel already completed, this is a guarded no-op.
        _ = _viewModel.CancelAsync();
    }
}
