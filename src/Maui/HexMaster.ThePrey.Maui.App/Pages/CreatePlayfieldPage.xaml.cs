using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class CreatePlayfieldPage : ContentPage
{
    public CreatePlayfieldPage(CreatePlayfieldViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
