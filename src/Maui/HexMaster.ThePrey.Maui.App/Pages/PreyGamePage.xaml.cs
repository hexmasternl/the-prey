using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using NetTopologySuite.Geometries;
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiColor = Mapsui.Styles.Color;
using MapsuiPen = Mapsui.Styles.Pen;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;

namespace HexMaster.ThePrey.Maui.App.Pages;

/// <summary>
/// The full-screen prey game play page. Mirrors <see cref="HunterGamePage"/> — a Mapsui
/// <see cref="MapControl"/> over OpenStreetMap with the playfield polygon, a compass-rotated green self
/// arrow, and player dots — but draws the playfield green and colours dots from the prey's perspective
/// (red hunter, green other-prey, grey caught). Translates <see cref="PreyGameViewModel.MapChanged"/> into
/// Mapsui layer redraws and drives the VM's activate/deactivate lifecycle.
/// </summary>
public partial class PreyGamePage : ContentPage
{
    private static readonly double StreetLevelResolution = 156543.03392804097 / Math.Pow(2, 15);

    private const double SelfArrowScale = 0.7;
    private const double BlipScale = 0.5;
    private const double PolygonOutlineWidth = 2;

    private readonly PreyGameViewModel _viewModel;
    private readonly MapControl _mapControl;

    private readonly MemoryLayer _polygonLayer = new("Playfield") { Style = null };
    private readonly MemoryLayer _blipLayer = new("Blips") { Style = null };
    private readonly MemoryLayer _selfLayer = new("Self") { Style = null };

    private bool _polygonDrawn;
    private bool _centeredOnce;
    private double _accumulatedHeading;

    public PreyGamePage(PreyGameViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;

        _mapControl = new MapControl
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
        _mapControl.Map.Layers.Add(_polygonLayer);
        _mapControl.Map.Layers.Add(_blipLayer);
        _mapControl.Map.Layers.Add(_selfLayer);

        MapHost.Children.Add(_mapControl);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.MapChanged += OnMapChanged;
        await _viewModel.ActivateAsync();
        RedrawMap();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.MapChanged -= OnMapChanged;
        _viewModel.Deactivate();
    }

    private void OnMapChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(RedrawMap);

    private void RedrawMap()
    {
        DrawPolygonOnce();
        DrawBlips();
        DrawSelf();
        _mapControl.RefreshGraphics();
    }

    /// <summary>Draws the green playfield polygon once; it never changes mid-game.</summary>
    private void DrawPolygonOnce()
    {
        if (_polygonDrawn)
            return;

        var vertices = _viewModel.PlayfieldPolygon;
        if (vertices.Count < 3)
            return;

        _polygonLayer.Features = [CreatePolygonFeature(vertices, GameMapPalette.Signal)];
        _polygonDrawn = true;
        CenterOnce(vertices);
    }

    private void DrawBlips()
    {
        var blips = _viewModel.Blips;
        var features = new IFeature[blips.Count];
        for (var i = 0; i < blips.Count; i++)
        {
            var blip = blips[i];
            features[i] = CreateDotFeature(blip.Latitude, blip.Longitude, GameMapPalette.PreyMapDotColor(blip.Role));
        }
        _blipLayer.Features = features;
    }

    private void DrawSelf()
    {
        if (_viewModel.SelfPosition is not { } fix)
        {
            _selfLayer.Features = [];
            return;
        }

        AccumulateHeading(_viewModel.Heading);

        var (x, y) = SphericalMercator.FromLonLat(fix.Longitude, fix.Latitude);
        var feature = new PointFeature(x, y);
        feature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Triangle,
            SymbolScale = SelfArrowScale,
            SymbolRotation = _accumulatedHeading,
            Fill = new MapsuiBrush(GameMapPalette.Signal),
            Outline = new MapsuiPen(GameMapPalette.Signal, 1)
        });
        _selfLayer.Features = [feature];

        if (!_centeredOnce)
            CenterOn(fix.Latitude, fix.Longitude);
    }

    /// <summary>Accumulates the heading so the arrow turns the short way across the 0°/360° seam.</summary>
    private void AccumulateHeading(double? heading)
    {
        if (heading is not { } target)
            return;

        var current = ((_accumulatedHeading % 360) + 360) % 360;
        var delta = ((target - current + 540) % 360) - 180;
        _accumulatedHeading += delta;
    }

    private void CenterOnce(IReadOnlyList<GpsCoordinate> vertices)
    {
        if (_centeredOnce || vertices.Count == 0)
            return;

        double lat = 0, lon = 0;
        foreach (var v in vertices)
        {
            lat += v.Latitude;
            lon += v.Longitude;
        }
        CenterOn(lat / vertices.Count, lon / vertices.Count);
    }

    private void CenterOn(double latitude, double longitude)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        _mapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), StreetLevelResolution);
        _centeredOnce = true;
    }

    private static PointFeature CreateDotFeature(double latitude, double longitude, MapsuiColor color)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        var feature = new PointFeature(x, y);
        feature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = BlipScale,
            Fill = new MapsuiBrush(color),
            Outline = new MapsuiPen(color, 1)
        });
        return feature;
    }

    private static GeometryFeature CreatePolygonFeature(IReadOnlyList<GpsCoordinate> vertices, MapsuiColor outline)
    {
        var ring = new Coordinate[vertices.Count + 1];
        for (var i = 0; i < vertices.Count; i++)
        {
            var (x, y) = SphericalMercator.FromLonLat(vertices[i].Longitude, vertices[i].Latitude);
            ring[i] = new Coordinate(x, y);
        }
        ring[^1] = ring[0];

        var feature = new GeometryFeature(new NtsPolygon(new LinearRing(ring)));
        feature.Styles.Add(new VectorStyle
        {
            Fill = new MapsuiBrush(GameMapPalette.PolygonFill(outline)),
            Outline = new MapsuiPen(outline, PolygonOutlineWidth)
        });
        return feature;
    }
}
