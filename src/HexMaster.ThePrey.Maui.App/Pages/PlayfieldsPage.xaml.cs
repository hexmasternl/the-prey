using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class PlayfieldsPage : ContentPage
{
    private readonly PlayFieldsListViewModel _viewModel;
    private readonly ICreatePlayfieldNavigator _createNavigator;
    private readonly IEditPlayfieldNavigator _editNavigator;

    public PlayfieldsPage(
        PlayFieldsListViewModel viewModel,
        ICreatePlayfieldNavigator createNavigator,
        IEditPlayfieldNavigator editNavigator)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _createNavigator = createNavigator;
        _editNavigator = editNavigator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Returning from a successful create: append the new playfield without a full reload.
        var created = _createNavigator.ConsumeCreated();
        if (created is not null)
        {
            _viewModel.AppendPrivate(created);
            return;
        }

        // Returning from a successful edit: replace the matching item in place.
        var updated = _editNavigator.ConsumeUpdated();
        if (updated is not null)
        {
            _viewModel.ReplacePrivate(updated);
            return;
        }

        await _viewModel.LoadPrivateAsync();
    }

    private async void OnCreateClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(ShellPlayfieldNavigator.CreatePlayfieldRoute);

    private async void OnPrivateItemTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not PlayFieldListItem item)
            return;

        await Shell.Current.GoToAsync(
            $"{ShellPlayfieldNavigator.EditPlayfieldRoute}?{EditPlayfieldPage.IdParameter}={item.Id}");
    }
}
