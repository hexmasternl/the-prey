using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Pages;

public partial class EditPlayfieldPage : ContentPage, IQueryAttributable
{
    /// <summary>Shell query key carrying the id of the playfield to edit.</summary>
    public const string IdParameter = "id";

    private readonly EditPlayfieldViewModel _viewModel;
    private Guid _id;
    private bool _loadStarted;

    public EditPlayfieldPage(EditPlayfieldViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(IdParameter, out var value) && Guid.TryParse(value?.ToString(), out var id))
            _id = id;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Load exactly once: returning from the area editor re-fires OnAppearing and must not wipe edits.
        if (_loadStarted)
            return;

        _loadStarted = true;
        await _viewModel.LoadAsync(_id);
    }
}
