using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the Edit Playfield page: loads the full playfield by id (capturing an immutable snapshot and the
/// <c>LastUpdatedOn</c> concurrency stamp), gates the Public/Private toggle by the same name pattern as
/// create, derives Save enablement from dirtiness-vs-snapshot AND validity, and performs the concurrency-
/// stamped update with its result mapping (including the 409 stale-write conflict + reload). Plain .NET
/// (HTTP/navigation behind interfaces) so it is fully unit-testable.
/// </summary>
public sealed class EditPlayfieldViewModel : ObservableObject
{
    // Tight epsilon for the order-sensitive polygon dirty compare; the editor returns the exact
    // coordinates it stored, so an unedited round-trip compares equal.
    private const double CoordinateEpsilon = 1e-9;

    private readonly IPlayFieldApiClient _api;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IEditPlayfieldNavigator _navigator;
    private readonly ILogger<EditPlayfieldViewModel> _logger;

    private Guid _id;
    private DateTimeOffset _lastUpdatedOn;

    // Immutable snapshot of the loaded state, for dirty comparison.
    private string _originalName = string.Empty;
    private bool _originalIsPublic;
    private IReadOnlyList<GpsCoordinate> _originalPolygon = [];

    // The current, in-progress state.
    private string _name = string.Empty;
    private bool _isPublic;
    private bool _canTogglePublic;
    private IReadOnlyList<GpsCoordinate> _polygon = [];

    // The server's current state carried on a 409, applied by Reload.
    private PlayFieldDetails? _conflictDetails;

    private bool _isBusy;
    private bool _isLoaded;
    private bool _hasLoadError;
    private bool _hasError;
    private bool _hasValidationError;
    private bool _hasConflict;

    public EditPlayfieldViewModel(
        IPlayFieldApiClient api,
        IAccessTokenProvider accessTokenProvider,
        IEditPlayfieldNavigator navigator,
        ILogger<EditPlayfieldViewModel> logger)
    {
        _api = api;
        _accessTokenProvider = accessTokenProvider;
        _navigator = navigator;
        _logger = logger;

        SetAreaCommand = new RelayCommand(SetAreaAsync);
        SaveCommand = new RelayCommand(SaveAsync, () => CanSave);
        CancelCommand = new RelayCommand(() => _navigator.ReturnToListWithUpdateAsync(null));
        ReloadCommand = new RelayCommand(ReloadAsync);
    }

    /// <summary>The editable name. Editing recomputes the toggle gate and Save enablement.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
                return;

            CanTogglePublic = PlayfieldNameValidator.IsPublishable(value);
            if (!CanTogglePublic && IsPublic)
                IsPublic = false;

            OnSaveInputsChanged();
        }
    }

    /// <summary>The chosen visibility. Meaningful only while <see cref="CanTogglePublic"/>.</summary>
    public bool IsPublic
    {
        get => _isPublic;
        set
        {
            if (SetProperty(ref _isPublic, value))
                OnSaveInputsChanged();
        }
    }

    /// <summary>True when the name matches the publishable pattern, enabling the Public/Private toggle.</summary>
    public bool CanTogglePublic
    {
        get => _canTogglePublic;
        private set => SetProperty(ref _canTogglePublic, value);
    }

    /// <summary>True when a polygon of at least three points is held.</summary>
    public bool HasArea => _polygon.Count >= DefineAreaViewModel.MinPoints;

    /// <summary>True when the current state differs from the loaded snapshot (name, visibility, or polygon).</summary>
    public bool IsDirty =>
        !string.Equals(Name, _originalName, StringComparison.Ordinal)
        || IsPublic != _originalIsPublic
        || !PolygonEquals(_polygon, _originalPolygon);

    /// <summary>True when the current state is itself valid (non-empty name, ≥ 3 points).</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && HasArea;

    /// <summary>Save is enabled only when something changed AND the result is valid.</summary>
    public bool CanSave => IsDirty && IsValid && !IsBusy;

    /// <summary>True while a load or save request is in flight.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanSave));
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>True once the playfield has loaded and the form is editable.</summary>
    public bool IsLoaded
    {
        get => _isLoaded;
        private set => SetProperty(ref _isLoaded, value);
    }

    /// <summary>True when the playfield could not be loaded (not found / session lost / error).</summary>
    public bool HasLoadError
    {
        get => _hasLoadError;
        private set => SetProperty(ref _hasLoadError, value);
    }

    /// <summary>True when the last save failed transiently (network, unexpected, forbidden, not-found, session lost).</summary>
    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    /// <summary>True when the backend rejected the update as invalid (the edits are kept).</summary>
    public bool HasValidationError
    {
        get => _hasValidationError;
        private set => SetProperty(ref _hasValidationError, value);
    }

    /// <summary>True when the update hit a stale-write conflict (409); Reload is then offered.</summary>
    public bool HasConflict
    {
        get => _hasConflict;
        private set => SetProperty(ref _hasConflict, value);
    }

    /// <summary>Opens the area editor with the held polygon (Cancel leaves it unchanged).</summary>
    public RelayCommand SetAreaCommand { get; }

    /// <summary>Saves the update (guarded by <see cref="CanSave"/>).</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>Closes the page without saving.</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>Reloads to the server's current state after a conflict, discarding pending edits.</summary>
    public RelayCommand ReloadCommand { get; }

    /// <summary>
    /// Loads the playfield by id: on success captures the snapshot + stamp and populates the form; on
    /// not-found / unauthorized / error shows a load-error state (the page is unusable for editing).
    /// </summary>
    public async Task LoadAsync(Guid id, CancellationToken ct = default)
    {
        _id = id;
        IsBusy = true;
        HasLoadError = false;
        IsLoaded = false;
        ClearSaveStates();
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is null)
            {
                HasLoadError = true;
                return;
            }

            var result = await _api.GetPlayFieldAsync(id, token, ct);
            switch (result.Outcome)
            {
                case GetPlayFieldOutcome.Success when result.PlayField is not null:
                    ApplyLoaded(result.PlayField);
                    IsLoaded = true;
                    break;

                case GetPlayFieldOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    HasLoadError = true;
                    break;

                default:
                    HasLoadError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load the playfield for editing.");
            HasLoadError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Populates the current state from a loaded/refreshed playfield and captures it as the new snapshot.
    // Backing fields are set directly (bypassing the Name setter's force-to-Private) so a server playfield
    // whose name doesn't match the client pattern keeps its stored visibility.
    private void ApplyLoaded(PlayFieldDetails details)
    {
        _lastUpdatedOn = details.LastUpdatedOn;

        _originalName = details.Name;
        _originalIsPublic = details.IsPublic;
        _originalPolygon = details.Points.ToList();

        _name = details.Name;
        _isPublic = details.IsPublic;
        _polygon = details.Points.ToList();
        _canTogglePublic = PlayfieldNameValidator.IsPublishable(details.Name);

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsPublic));
        OnPropertyChanged(nameof(CanTogglePublic));
        OnPropertyChanged(nameof(HasArea));
        OnSaveInputsChanged();
        ClearSaveStates();
    }

    private async Task SetAreaAsync()
    {
        var result = await _navigator.EditAreaAsync(_polygon);
        if (result is not null)
        {
            _polygon = result;
            OnPropertyChanged(nameof(HasArea));
            OnSaveInputsChanged();
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
            return;

        IsBusy = true;
        ClearSaveStates();
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync();
            if (token is null)
            {
                HasError = true;
                return;
            }

            var result = await _api.UpdatePlayFieldAsync(_id, Name.Trim(), IsPublic, _polygon, _lastUpdatedOn, token);
            switch (result.Outcome)
            {
                case UpdatePlayFieldOutcome.Updated:
                    await _navigator.ReturnToListWithUpdateAsync(result.Summary);
                    break;

                case UpdatePlayFieldOutcome.Conflict:
                    _conflictDetails = result.Current;
                    HasConflict = true;
                    break;

                case UpdatePlayFieldOutcome.Validation:
                    HasValidationError = true;
                    break;

                case UpdatePlayFieldOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    HasError = true;
                    break;

                default:
                    // Forbidden / NotFound / Error — surface without crashing; the user may retry or back out.
                    HasError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update the playfield.");
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Reset to the server's current state carried on the 409 (or re-fetch if it wasn't captured).
    private async Task ReloadAsync()
    {
        if (_conflictDetails is { } current)
        {
            ApplyLoaded(current);
            _conflictDetails = null;
            return;
        }

        await LoadAsync(_id);
    }

    private void OnSaveInputsChanged()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.RaiseCanExecuteChanged();
    }

    private void ClearSaveStates()
    {
        HasError = false;
        HasValidationError = false;
        HasConflict = false;
    }

    private static bool PolygonEquals(IReadOnlyList<GpsCoordinate> a, IReadOnlyList<GpsCoordinate> b)
    {
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (Math.Abs(a[i].Latitude - b[i].Latitude) > CoordinateEpsilon ||
                Math.Abs(a[i].Longitude - b[i].Longitude) > CoordinateEpsilon)
                return false;
        }

        return true;
    }
}
