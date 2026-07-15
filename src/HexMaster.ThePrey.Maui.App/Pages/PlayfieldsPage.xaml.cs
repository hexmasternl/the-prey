using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class PlayfieldsPage : ContentPage
{
    private readonly PlayFieldsListViewModel _viewModel;

    public PlayfieldsPage(PlayFieldsListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadPrivateAsync();
    }
}
