using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using NetTopologySuite.Geometries;
using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;
using MBrush = Mapsui.Styles.Brush;
using MColor = Mapsui.Styles.Color;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;

namespace ThePrey.Application.App;

[QueryProperty(nameof(PlayfieldId), "id")]
[QueryProperty(nameof(IsReadOnly), "readonly")]
public partial class PlayfieldDetailsPage : ContentPage
{
    // ─── Design-system colours (mirror area editor) ──────────────────────────
    private static readonly MColor SignalGreen  = new(100, 255, 0, 255);
    private static readonly MColor SignalFill50 = new(100, 255, 0, 128); // 50 % alpha

    // ─── State ───────────────────────────────────────────────────────────────
    private readonly IPlayfieldService _service;
    private readonly PlayfieldCacheService _cache;
    private readonly PlayfieldEditingContext _editingContext;

    private List<PlayfieldCoordinate> _coordinates = [];
    private PlayfieldCoordinate? _centerCoordinates;
    private bool _isInitialized;
    private WritableLayer? _miniShapeLayer;

    public string? PlayfieldId { get; set; }
    public string? IsReadOnly { get; set; }

    private bool ReadOnly =>
        string.Equals(IsReadOnly, "true", StringComparison.OrdinalIgnoreCase);

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
        VisibilityLabel.Text     = AppLocalizer.VisibilityLabel;
        AreaLabel.Text           = AppLocalizer.AreaLabel;
        SetAreaButton.Text       = AppLocalizer.SetAreaButton;
        SaveToolbarItem.Text     = AppLocalizer.SaveButton;
        LocationNoticeLabel.Text = AppLocalizer.LocationUnavailableNotice;

        InitializeMiniMap();
    }

    // ─── Mini-map init ───────────────────────────────────────────────────────

    private void InitializeMiniMap()
    {
        var map = new Mapsui.Map();
        map.Widgets.Clear();
        map.Layers.Add(OpenStreetMap.CreateTileLayer("ThePrey/1.0"));

        _miniShapeLayer = new WritableLayer { Name = "Shape" };
        map.Layers.Add(_miniShapeLayer);

        // Default home — Amsterdam; overridden once data or location is known.
        var (ax, ay) = SphericalMercator.FromLonLat(4.9041, 52.3676);
        var fallback = SquareExtent(ax, ay, 5000);
        map.ViewportInitialized += (_, _) => FitMiniMap(fallbackExtent: fallback);

        MiniMap.Map = map;
        // No Info handler → mini-map is display-only.
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
            // Returning from area editor — pick up coordinate changes.
            _coordinates = [.. _editingContext.CurrentCoordinates];
            UpdateMiniMap();
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
                if (ReadOnly) { Title ??= AppLocalizer.ViewPlayfieldTitle; return; }
                await DisplayAlertAsync(AppLocalizer.Error, AppLocalizer.PlayfieldNotFoundError, AppLocalizer.Ok);
                await Shell.Current.GoToAsync("..");
                return;
            }

            if (!ReadOnly) Title = AppLocalizer.EditPlayfieldTitle;
            NameEntry.Text = playfield.Name;
            PublicSwitch.IsToggled = playfield.IsPublic;
            _coordinates = [.. playfield.Coordinates];
            _centerCoordinates = playfield.CenterCoordinates ?? playfield.ComputeCenter();
        }
        else
        {
            if (!ReadOnly) Title = AppLocalizer.NewPlayfieldTitle;
            _coordinates = [];
        }

        _editingContext.CurrentCoordinates = [.. _coordinates];

        UpdateMiniMap();

        if (_coordinates.Count == 0)
            _ = CenterMiniMapOnUserAsync();

        UpdateSaveButton();
    }

    private void ApplyReadOnlyMode()
    {
        NameEntry.IsEnabled = false;
        PublicSwitch.IsEnabled = false;
        SetAreaButton.IsEnabled = false;
        ToolbarItems.Remove(SaveToolbarItem);
    }

    // ─── Mini-map drawing ────────────────────────────────────────────────────

    private void UpdateMiniMap()
    {
        if (_miniShapeLayer is null) return;

        // Recompute center from current coordinates so the map always centers correctly.
        if (_coordinates.Count > 0)
        {
            var lat = _coordinates.Average(c => c.Latitude);
            var lon = _coordinates.Average(c => c.Longitude);
            _centerCoordinates = new PlayfieldCoordinate { Latitude = lat, Longitude = lon };
        }

        _miniShapeLayer.Clear();

        if (_coordinates.Count >= 2)
        {
            var projected = _coordinates
                .Select(c => { var (x, y) = SphericalMercator.FromLonLat(c.Longitude, c.Latitude); return new Coordinate(x, y); })
                .ToArray();

            GeometryFeature feature;
            if (_coordinates.Count >= 3)
            {
                var ring = new LinearRing([.. projected, projected[0]]);
                feature = new GeometryFeature { Geometry = new NtsPolygon(ring) };
                feature.Styles.Add(new VectorStyle
                {
                    Fill    = new MBrush(SignalFill50),   // 50 % transparent fill
                    Outline = new Pen(SignalGreen, 2)
                });
            }
            else
            {
                feature = new GeometryFeature { Geometry = new LineString(projected) };
                feature.Styles.Add(new VectorStyle { Fill = null, Line = new Pen(SignalGreen, 2) });
            }

            _miniShapeLayer.Add(feature);
        }

        FitMiniMap(fallbackExtent: null);
        MiniMap.Map.RefreshGraphics();
    }

    /// <summary>Fits the mini-map viewport to the shape bounds (centered on CenterCoordinates), or the fallback extent when empty.</summary>
    private void FitMiniMap(MRect? fallbackExtent)
    {
        if (_coordinates.Count > 0)
        {
            var bbox = BoundsOf(_coordinates, 0.2);
            if (_centerCoordinates is { } center)
            {
                var (cx, cy) = SphericalMercator.FromLonLat(center.Longitude, center.Latitude);
                var zoom = Math.Max(bbox.Width, bbox.Height);
                var centered = new MRect(cx - zoom / 2, cy - zoom / 2, cx + zoom / 2, cy + zoom / 2);
                MiniMap.Map.Navigator.ZoomToBox(centered, MBoxFit.Fit, 0, null);
            }
            else
            {
                MiniMap.Map.Navigator.ZoomToBox(bbox, MBoxFit.Fit, 0, null);
            }
        }
        else if (fallbackExtent is not null)
        {
            MiniMap.Map.Navigator.ZoomToBox(fallbackExtent, MBoxFit.Fit, 0, null);
        }
    }

    private async Task CenterMiniMapOnUserAsync()
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

            if (loc is null)
            {
                await MainThread.InvokeOnMainThreadAsync(() => LocationNoticeLabel.IsVisible = true);
                return;
            }

            var (x, y) = SphericalMercator.FromLonLat(loc.Longitude, loc.Latitude);
            var extent = SquareExtent(x, y, 1000);
            await MainThread.InvokeOnMainThreadAsync(() =>
                MiniMap.Map.Navigator.ZoomToBox(extent, MBoxFit.Fit, 0, null));
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() => LocationNoticeLabel.IsVisible = true);
        }
    }

    // ─── Geometry utilities ───────────────────────────────────────────────────

    private static MRect BoundsOf(List<PlayfieldCoordinate> coords, double padding = 0.1)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var c in coords)
        {
            var (x, y) = SphericalMercator.FromLonLat(c.Longitude, c.Latitude);
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
        var px = Math.Max((maxX - minX) * padding, 100);
        var py = Math.Max((maxY - minY) * padding, 100);
        return new MRect(minX - px, minY - py, maxX + px, maxY + py);
    }

    private static MRect SquareExtent(double cx, double cy, double meters)
        => new(cx - meters, cy - meters, cx + meters, cy + meters);

    // ─── Validation ──────────────────────────────────────────────────────────

    private bool IsFormValid =>
        (NameEntry.Text?.Length ?? 0) >= 5 && _coordinates.Count >= 3;

    private void UpdateSaveButton()
    {
        if (!ReadOnly) SaveToolbarItem.IsEnabled = IsFormValid;
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
        _editingContext.CenterCoordinates = _centerCoordinates;
        await Shell.Current.GoToAsync(AppShell.PlayfieldAreaEditorRoute);
    }

    // ─── Save ────────────────────────────────────────────────────────────────

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var playfield = new Playfield
        {
            Id             = string.IsNullOrEmpty(PlayfieldId) ? Guid.NewGuid().ToString() : PlayfieldId!,
            Name           = NameEntry.Text?.Trim() ?? string.Empty,
            IsPublic       = PublicSwitch.IsToggled,
            Coordinates    = [.. _coordinates],
            LastUpdatedOn  = DateTimeOffset.UtcNow,
            IsSynchronized = false
        };
        playfield.CenterCoordinates = playfield.ComputeCenter();

        await _cache.UpsertAsync(playfield);

        if (Connectivity.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                var synced = await _service.UpsertPlayfieldAsync(playfield);
                synced.IsSynchronized = true;
                await _cache.UpsertAsync(synced);
                await Shell.Current.GoToAsync($"../{AppShell.PlayfieldsRoute}");
                return;
            }
            catch (StaleWriteException ex)
            {
                var serverCopy = ex.ServerCopy;
                serverCopy.IsSynchronized = true;
                await _cache.UpsertAsync(serverCopy);
                await Shell.Current.GoToAsync($"../{AppShell.PlayfieldsRoute}");
                return;
            }
            catch (UnauthorizedException)
            {
                await Shell.Current.GoToAsync(AppShell.LoginRoute);
                return;
            }
            catch
            {
                // Upload failed — data is safe in cache; fall through to show pending message.
            }
        }

        await DisplayAlertAsync(AppLocalizer.Error, AppLocalizer.SavedLocallyPending, AppLocalizer.Ok);
        await Shell.Current.GoToAsync($"../{AppShell.PlayfieldsRoute}");
    }
}
