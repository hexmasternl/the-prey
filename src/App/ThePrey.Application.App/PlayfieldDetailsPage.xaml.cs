using System.Text.Json;
using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

[QueryProperty(nameof(PlayfieldId), "id")]
[QueryProperty(nameof(IsReadOnly), "readonly")]
public partial class PlayfieldDetailsPage : ContentPage
{
    private readonly IPlayfieldService _service;
    private readonly PlayfieldCacheService _cache;
    private readonly PlayfieldEditingContext _editingContext;

    private List<PlayfieldCoordinate> _coordinates = [];
    private bool _isInitialized;
    private bool _mapReady;
    private (double Lat, double Lon)? _userLocation;

    public string? PlayfieldId { get; set; }
    public string? IsReadOnly { get; set; }

    private bool ReadOnly => string.Equals(IsReadOnly, "true", StringComparison.OrdinalIgnoreCase);

    public PlayfieldDetailsPage(
        IPlayfieldService service,
        PlayfieldCacheService cache,
        PlayfieldEditingContext editingContext)
    {
        InitializeComponent();
        _service = service;
        _cache = cache;
        _editingContext = editingContext;

        NameValidationLabel.Text = AppLocalizer.NameValidationMessage;
        VisibilityLabel.Text = AppLocalizer.VisibilityLabel;
        AreaLabel.Text = AppLocalizer.AreaLabel;
        SetAreaButton.Text = AppLocalizer.SetAreaButton;
        SaveToolbarItem.Text = AppLocalizer.SaveButton;
        LocationNoticeLabel.Text = AppLocalizer.LocationUnavailableNotice;
    }

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            _isInitialized = true;
            await InitializeFormAsync();
        }
        else
        {
            // Returning from area editor — pick up any coordinate changes
            _coordinates = [.. _editingContext.CurrentCoordinates];
            await SendMapInitAsync();
            UpdateSaveButton();
        }
    }

    private async Task InitializeFormAsync()
    {
        if (ReadOnly)
        {
            Title = AppLocalizer.ViewPlayfieldTitle;
            ApplyReadOnlyMode();
        }

        if (!string.IsNullOrEmpty(PlayfieldId))
        {
            var all = await _cache.LoadAsync();
            var playfield = all.FirstOrDefault(p => p.Id == PlayfieldId);

            if (playfield is null)
            {
                if (ReadOnly)
                {
                    Title ??= AppLocalizer.ViewPlayfieldTitle;
                    return;
                }
                await DisplayAlertAsync(AppLocalizer.Error, AppLocalizer.PlayfieldNotFoundError, AppLocalizer.Ok);
                await Shell.Current.GoToAsync("..");
                return;
            }

            if (!ReadOnly) Title = AppLocalizer.EditPlayfieldTitle;
            NameEntry.Text = playfield.Name;
            PublicSwitch.IsToggled = playfield.IsPublic;
            _coordinates = [.. playfield.Coordinates];
        }
        else
        {
            if (!ReadOnly) Title = AppLocalizer.NewPlayfieldTitle;
            _coordinates = [];
        }

        _editingContext.CurrentCoordinates = [.. _coordinates];

        if (_coordinates.Count == 0)
            _ = FetchUserLocationAsync();

        await SendMapInitAsync();
        UpdateSaveButton();
    }

    private void ApplyReadOnlyMode()
    {
        NameEntry.IsEnabled = false;
        PublicSwitch.IsEnabled = false;
        SetAreaButton.IsEnabled = false;
        ToolbarItems.Remove(SaveToolbarItem);
    }

    // ─── Mini-map (HybridWebView + Leaflet) ─────────────────────────────────

    private void OnMiniMapMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Message)) return;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(e.Message); } catch { return; }

        if (doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "ready")
        {
            _mapReady = true;
            _ = SendMapInitAsync();
        }
    }

    private async Task SendMapInitAsync()
    {
        if (!_mapReady) return;

        object? center = null;
        if (_coordinates.Count == 0 && _userLocation.HasValue)
            center = new { lat = _userLocation.Value.Lat, lon = _userLocation.Value.Lon };

        var payload = new
        {
            type = "init",
            coordinates = _coordinates.Select(c => new { lat = c.Latitude, lon = c.Longitude }),
            center
        };

        var json = JsonSerializer.Serialize(payload);
        await MainThread.InvokeOnMainThreadAsync(() => MiniMap.SendRawMessage(json));
    }

    private async Task FetchUserLocationAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                await MainThread.InvokeOnMainThreadAsync(() => LocationNoticeLabel.IsVisible = true);
                return;
            }

            var loc = await Geolocation.GetLastKnownLocationAsync()
                ?? await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(5)));

            if (loc is not null)
            {
                _userLocation = (loc.Latitude, loc.Longitude);
                await SendMapInitAsync();
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() => LocationNoticeLabel.IsVisible = true);
            }
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() => LocationNoticeLabel.IsVisible = true);
        }
    }

    // ─── Validation ──────────────────────────────────────────────────────────

    private bool IsFormValid =>
        (NameEntry.Text?.Length ?? 0) >= 5 && _coordinates.Count >= 3;

    private void UpdateSaveButton()
    {
        if (!ReadOnly)
            SaveToolbarItem.IsEnabled = IsFormValid;
    }

    private void OnNameTextChanged(object? sender, TextChangedEventArgs e) => UpdateSaveButton();

    private void OnNameUnfocused(object? sender, FocusEventArgs e)
    {
        var len = NameEntry.Text?.Length ?? 0;
        NameValidationLabel.IsVisible = len > 0 && len < 5;
    }

    // ─── Set Area ────────────────────────────────────────────────────────────

    private async void OnSetAreaClicked(object? sender, EventArgs e)
    {
        _editingContext.CurrentCoordinates = [.. _coordinates];
        await Shell.Current.GoToAsync(AppShell.PlayfieldAreaEditorRoute);
    }

    // ─── Save ────────────────────────────────────────────────────────────────

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var isNew = string.IsNullOrEmpty(PlayfieldId);
        var playfield = new Playfield
        {
            Id = isNew ? Guid.NewGuid().ToString() : PlayfieldId!,
            Name = NameEntry.Text?.Trim() ?? string.Empty,
            IsPublic = PublicSwitch.IsToggled,
            Coordinates = [.. _coordinates]
        };

        await _cache.UpsertAsync(playfield);

        try
        {
            if (isNew)
                await _service.CreatePlayfieldAsync(playfield);
            else
                await _service.UpdatePlayfieldAsync(playfield);

            await Shell.Current.GoToAsync($"../{AppShell.PlayfieldsRoute}");
        }
        catch (UnauthorizedException)
        {
            await Shell.Current.GoToAsync(AppShell.LoginRoute);
        }
        catch
        {
            await DisplayAlertAsync(AppLocalizer.Error, AppLocalizer.SaveError, AppLocalizer.Ok);
        }
    }
}
