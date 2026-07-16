using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class StartGamePage : ContentPage
{
    public StartGamePage(StartGameViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
