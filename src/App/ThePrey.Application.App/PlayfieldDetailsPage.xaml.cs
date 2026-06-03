using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;
using Map = Microsoft.Maui.Controls.Maps.Map;

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
            UpdateMap();
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
                    // Public playfield — not in local cache; show placeholder
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

        // Seed the editing context so Set Area starts with current coordinates
        _editingContext.CurrentCoordinates = [.. _coordinates];

        UpdateMap();
        UpdateSaveButton();
    }

    private void ApplyReadOnlyMode()
    {
        NameEntry.IsEnabled = false;
        PublicSwitch.IsEnabled = false;
        SetAreaButton.IsEnabled = false;
        SaveToolbarItem.IsEnabled = false;
        SaveToolbarItem.IsDestructive = false;
        // Hide Save from toolbar in a readable way — set text to empty so it takes no space
        ToolbarItems.Remove(SaveToolbarItem);
    }

    // ─── Map rendering ───────────────────────────────────────────────────────

    private async void UpdateMap()
    {
        MiniMap.MapElements.Clear();
        MiniMap.Pins.Clear();

        if (_coordinates.Count > 0)
        {
            var polygon = new Polygon
            {
                StrokeColor = Color.FromArgb("#64FF00"),
                StrokeWidth = 2,
                FillColor = Color.FromArgb("#2064FF00")
            };
            foreach (var c in _coordinates)
                polygon.Geopath.Add(new Location(c.Latitude, c.Longitude));
            MiniMap.MapElements.Add(polygon);

            var center = CalculateCenter(_coordinates);
            var radiusKm = CalculateRadiusKm(_coordinates, center);
            MiniMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(center.Latitude, center.Longitude),
                Distance.FromKilometers(Math.Max(radiusKm * 1.5, 0.1))));
        }
        else
        {
            await CenterOnUserLocationAsync();
        }
    }

    private async Task CenterOnUserLocationAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                ShowLocationNotice();
                return;
            }

            var location = await Geolocation.GetLastKnownLocationAsync()
                ?? await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(5)));

            if (location is not null)
            {
                LocationNoticeLabel.IsVisible = false;
                MiniMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(location.Latitude, location.Longitude),
                    Distance.FromKilometers(0.5)));
            }
            else
            {
                ShowLocationNotice();
            }
        }
        catch
        {
            ShowLocationNotice();
        }
    }

    private void ShowLocationNotice()
    {
        LocationNoticeLabel.IsVisible = true;
        MiniMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(52.3676, 4.9041), // Amsterdam fallback
            Distance.FromKilometers(10)));
    }

    private static PlayfieldCoordinate CalculateCenter(List<PlayfieldCoordinate> coords)
    {
        var lat = coords.Average(c => c.Latitude);
        var lon = coords.Average(c => c.Longitude);
        return new PlayfieldCoordinate { Latitude = lat, Longitude = lon };
    }

    private static double CalculateRadiusKm(List<PlayfieldCoordinate> coords, PlayfieldCoordinate center)
    {
        double maxDistKm = 0;
        foreach (var c in coords)
        {
            var dLat = (c.Latitude - center.Latitude) * 111.0;
            var dLon = (c.Longitude - center.Longitude) * 111.0 * Math.Cos(center.Latitude * Math.PI / 180);
            var dist = Math.Sqrt(dLat * dLat + dLon * dLon);
            if (dist > maxDistKm) maxDistKm = dist;
        }
        return maxDistKm;
    }

    // ─── Validation ──────────────────────────────────────────────────────────

    private bool IsFormValid =>
        (NameEntry.Text?.Length ?? 0) >= 5 && _coordinates.Count >= 3;

    private void UpdateSaveButton()
    {
        if (!ReadOnly)
            SaveToolbarItem.IsEnabled = IsFormValid;
    }

    private void OnNameTextChanged(object? sender, TextChangedEventArgs e)
        => UpdateSaveButton();

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
