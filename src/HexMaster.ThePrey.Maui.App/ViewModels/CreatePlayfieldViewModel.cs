using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the Create Playfield page: the name field (whose pattern gates the Public/Private toggle via
/// <see cref="PlayfieldNameValidator"/>), the held polygon defined in the area editor, Save enablement
/// (name non-empty AND a ≥ 3-point area), and the authenticated create call with its result mapping.
/// Plain .NET (HTTP/navigation behind interfaces) so it is fully unit-testable.
/// </summary>
public sealed class CreatePlayfieldViewModel : ObservableObject
{
    private readonly IPlayFieldApiClient _api;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ICreatePlayfieldNavigator _navigator;
    private readonly ILogger<CreatePlayfieldViewModel> _logger;

    private string _name = string.Empty;
    private bool _isPublic;
    private bool _canTogglePublic;
    private IReadOnlyList<GpsCoordinate> _polygon = [];
    private bool _isBusy;
    private bool _hasError;
    private bool _hasValidationError;

    public CreatePlayfieldViewModel(
        IPlayFieldApiClient api,
        IAccessTokenProvider accessTokenProvider,
        ICreatePlayfieldNavigator navigator,
        ILogger<CreatePlayfieldViewModel> logger)
    {
        _api = api;
        _accessTokenProvider = accessTokenProvider;
        _navigator = navigator;
        _logger = logger;

        DefineAreaCommand = new RelayCommand(DefineAreaAsync);
        SaveCommand = new RelayCommand(SaveAsync, () => CanSave && !IsBusy);
        CancelCommand = new RelayCommand(() => _navigator.ReturnToListAsync(null));
    }

    /// <summary>The playfield name. Editing recomputes the toggle gate and Save enablement.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
                return;

            CanTogglePublic = PlayfieldNameValidator.IsPublishable(value);
            // A name that no longer matches the pattern forces the visibility back to Private.
            if (!CanTogglePublic && IsPublic)
                IsPublic = false;

            OnSaveInputsChanged();
        }
    }

    /// <summary>The chosen visibility sent on save. Meaningful only while <see cref="CanTogglePublic"/>.</summary>
    public bool IsPublic
    {
        get => _isPublic;
        set => SetProperty(ref _isPublic, value);
    }

    /// <summary>True when the name matches the publishable pattern, enabling the Public/Private toggle.</summary>
    public bool CanTogglePublic
    {
        get => _canTogglePublic;
        private set => SetProperty(ref _canTogglePublic, value);
    }

    /// <summary>True when a polygon of at least three points has been defined.</summary>
    public bool HasArea => _polygon.Count >= DefineAreaViewModel.MinPoints;

    /// <summary>True when the name is non-empty and an area has been defined.</summary>
    public bool CanSave => !string.IsNullOrWhiteSpace(Name) && HasArea;

    /// <summary>True while the create request is in flight.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                SaveCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>True when the last save failed transiently (network, unexpected status, session lost).</summary>
    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    /// <summary>True when the backend rejected the create as invalid (the name and area are kept).</summary>
    public bool HasValidationError
    {
        get => _hasValidationError;
        private set => SetProperty(ref _hasValidationError, value);
    }

    /// <summary>Opens the area editor with the held polygon and stores the edited result.</summary>
    public RelayCommand DefineAreaCommand { get; }

    /// <summary>Creates the playfield (guarded by <see cref="CanSave"/>).</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>Closes the page without creating anything.</summary>
    public RelayCommand CancelCommand { get; }

    private async Task DefineAreaAsync()
    {
        var result = await _navigator.DefineAreaAsync(_polygon);
        // A cancel returns null: leave the previously held polygon unchanged.
        if (result is not null)
            SetPolygon(result);
    }

    private void SetPolygon(IReadOnlyList<GpsCoordinate> polygon)
    {
        _polygon = polygon;
        OnPropertyChanged(nameof(HasArea));
        OnSaveInputsChanged();
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
            return;

        IsBusy = true;
        HasError = false;
        HasValidationError = false;
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync();
            if (token is null)
            {
                HasError = true;
                return;
            }

            var result = await _api.CreatePlayFieldAsync(Name.Trim(), IsPublic, _polygon, token);
            switch (result.Outcome)
            {
                case CreatePlayFieldOutcome.Success:
                    await _navigator.ReturnToListAsync(result.PlayField);
                    break;

                case CreatePlayFieldOutcome.Validation:
                    HasValidationError = true;
                    break;

                case CreatePlayFieldOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    HasError = true;
                    break;

                default:
                    HasError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create the playfield.");
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSaveInputsChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.RaiseCanExecuteChanged();
    }
}
