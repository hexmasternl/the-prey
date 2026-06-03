using Mapsui;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using NetTopologySuite.Geometries;
using ThePrey.Application.App.Models;
using ThePrey.Application.App.Services;
using MBrush = Mapsui.Styles.Brush;
using MColor = Mapsui.Styles.Color;
using NtsPoint = NetTopologySuite.Geometries.Point;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;

namespace ThePrey.Application.App;

public partial class PlayfieldAreaEditorPage : ContentPage
{
    // ─── Design-system colours ───────────────────────────────────────────────
    private static readonly MColor SignalGreen  = new(100, 255, 0, 255);
    private static readonly MColor SignalFill50 = new(100, 255, 0, 128); // 50 % alpha
    private static readonly MColor DarkRim      = new(57,  64, 47, 255); // #39402F

    // ─── State ───────────────────────────────────────────────────────────────
    private readonly PlayfieldEditingContext _editingContext;
    private List<PlayfieldCoordinate> _coordinates = [];
    private WritableLayer? _pointLayer;
    private WritableLayer? _shapeLayer;

    public PlayfieldAreaEditorPage(PlayfieldEditingContext editingContext)
    {
        InitializeComponent();
        _editingContext = editingContext;

        CancelButton.Text = AppLocalizer.Cancel;
        OkButton.Text     = AppLocalizer.Ok;
    }

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _coordinates = [.. _editingContext.CurrentCoordinates];
        OkButton.IsEnabled = false;

        InitializeMap();
    }

    // ─── Map setup ───────────────────────────────────────────────────────────

    private void InitializeMap()
    {
        var map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer("ThePrey/1.0"));

        _shapeLayer = new WritableLayer { Name = "Shape" };
        map.Layers.Add(_shapeLayer);

        _pointLayer = new WritableLayer { Name = "Points" };
        map.Layers.Add(_pointLayer);

        // Initial viewport: center on CenterCoordinates when editing existing coordinates;
        // otherwise center on device location for a new playfield.
        if (_coordinates.Count > 0)
        {
            var center = _editingContext.CenterCoordinates;
            if (center is not null)
            {
                var bbox = BoundsOf(_coordinates, padding: 0.2);
                var (cx, cy) = SphericalMercator.FromLonLat(center.Longitude, center.Latitude);
                var zoom = Math.Max(bbox.Width, bbox.Height);
                var centeredExtent = new MRect(cx - zoom / 2, cy - zoom / 2, cx + zoom / 2, cy + zoom / 2);
                map.ViewportInitialized += (_, _) =>
                    map.Navigator.ZoomToBox(centeredExtent, MBoxFit.Fit, 0, null);
            }
            else
            {
                var bbox = BoundsOf(_coordinates, padding: 0.2);
                map.ViewportInitialized += (_, _) =>
                    map.Navigator.ZoomToBox(bbox, MBoxFit.Fit, 0, null);
            }
        }
        else
        {
            map.ViewportInitialized += (_, _) =>
                map.Navigator.ZoomToBox(DefaultExtent(), MBoxFit.Fit, 0, null);
            _ = CenterOnUserAsync(map);
        }

        MapControl.Map = map;
        map.Info += OnMapInfo;

        if (_coordinates.Count > 0)
            Redraw();
    }

    // ─── Map info / tap ──────────────────────────────────────────────────────

    private void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (e.GestureType != GestureType.SingleTap) return;
        if (e.WorldPosition is not { } tapped) return;

        // Hit-test the point layer: tap on an existing marker removes it.
        var mapInfo = e.GetMapInfo([_pointLayer!]);
        if (mapInfo?.Feature is GeometryFeature pf && pf["Index"] is int idx
            && idx >= 0 && idx < _coordinates.Count)
        {
            _coordinates.RemoveAt(idx);
            Redraw();
            return;
        }

        // Tap on empty map (or inside the polygon fill) → add a new point.
        var (lon, lat) = SphericalMercator.ToLonLat(tapped.X, tapped.Y);
        _coordinates.Add(new PlayfieldCoordinate { Latitude = lat, Longitude = lon });
        Redraw();
    }

    // ─── Drawing ─────────────────────────────────────────────────────────────

    private void Redraw()
    {
        DrawPoints();
        DrawShape();
        MapControl.Map.RefreshGraphics();
        MainThread.BeginInvokeOnMainThread(() =>
            OkButton.IsEnabled = _coordinates.Count >= 3);
    }

    private void DrawPoints()
    {
        _pointLayer!.Clear();
        for (int i = 0; i < _coordinates.Count; i++)
        {
            var (x, y) = SphericalMercator.FromLonLat(_coordinates[i].Longitude, _coordinates[i].Latitude);
            var feature = new GeometryFeature { Geometry = new NtsPoint(x, y) };
            feature["Index"] = i;
            feature.Styles.Add(new SymbolStyle
            {
                SymbolType  = SymbolType.Ellipse,
                SymbolScale = 0.45,
                Fill        = new MBrush(SignalGreen),
                Outline     = new Pen(DarkRim, 2)
            });
            _pointLayer.Add(feature);
        }
    }

    private void DrawShape()
    {
        _shapeLayer!.Clear();
        if (_coordinates.Count < 2) return;

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
                Fill    = new MBrush(SignalFill50),   // 50 % transparent signal-green fill
                Outline = new Pen(SignalGreen, 2)
            });
        }
        else
        {
            feature = new GeometryFeature { Geometry = new LineString(projected) };
            feature.Styles.Add(new VectorStyle
            {
                Fill = null,
                Line = new Pen(SignalGreen, 2)
            });
        }

        _shapeLayer.Add(feature);
    }

    // ─── Location helpers ────────────────────────────────────────────────────

    private static async Task CenterOnUserAsync(Mapsui.Map map)
    {
        var loc = await GetUserLocationAsync();
        if (loc is null) return;

        var (x, y) = SphericalMercator.FromLonLat(loc.Longitude, loc.Latitude);
        var extent = SquareExtent(x, y, meters: 1500);
        await MainThread.InvokeOnMainThreadAsync(() =>
            map.Navigator.ZoomToBox(extent, MBoxFit.Fit, 0, null));
    }

    private static async Task<Microsoft.Maui.Devices.Sensors.Location?> GetUserLocationAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted) return null;
            return await Geolocation.GetLastKnownLocationAsync()
                ?? await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(5)));
        }
        catch { return null; }
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
        var padX = Math.Max((maxX - minX) * padding, 100);
        var padY = Math.Max((maxY - minY) * padding, 100);
        return new MRect(minX - padX, minY - padY, maxX + padX, maxY + padY);
    }

    private static MRect SquareExtent(double centerX, double centerY, double meters)
        => new(centerX - meters, centerY - meters, centerX + meters, centerY + meters);

    private static MRect DefaultExtent()
    {
        // Amsterdam as fallback
        var (x, y) = SphericalMercator.FromLonLat(4.9041, 52.3676);
        return SquareExtent(x, y, 5000);
    }

    // ─── Cancel / OK ─────────────────────────────────────────────────────────

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnOkClicked(object? sender, EventArgs e)
    {
        _editingContext.CurrentCoordinates = [.. _coordinates];
        await Shell.Current.GoToAsync("..");
    }
}
