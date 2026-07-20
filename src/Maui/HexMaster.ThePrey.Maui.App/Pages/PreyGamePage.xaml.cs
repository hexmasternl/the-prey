using HexMaster.ThePrey.Maui.App.Controls;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
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

    private const double BlipScale = 0.5;
    private const double PolygonOutlineWidth = 2;

    private readonly PreyGameViewModel _viewModel;
    private readonly GameHudViewModel _hudViewModel;
    private readonly IMapCameraController _camera;
    private readonly MapControl _mapControl;

    private readonly MemoryLayer _polygonLayer = new("Playfield") { Style = null };
    private readonly MemoryLayer _blipLayer = new("Blips") { Style = null };
    private readonly MemoryLayer _selfLayer = new("Self") { Style = null };

    private bool _polygonDrawn;
    private bool _centeredOnce;
    private double _accumulatedHeading;

    public PreyGamePage(
        PreyGameViewModel viewModel,
        GameHudView hudView,
        GameHudViewModel hudViewModel,
        IMapCameraController camera)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _hudViewModel = hudViewModel;
        _camera = camera;
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

        // Mount the shared HUD overlay into the page's reserved region.
        hudView.BindingContext = hudViewModel;
        HudRegion.Content = hudView;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.MapChanged += OnMapChanged;
        _camera.FollowModeChanged += OnFollowModeChanged;
        await _viewModel.ActivateAsync();
        RedrawMap();

        // The map VM started the shared game-state store; activate the HUD onto the same game so it reads
        // the same snapshots. Skip when no game resolved (the error overlay is shown instead).
        if (_viewModel.GameId != Guid.Empty && !_viewModel.HasError)
        {
            _hudViewModel.Initialize(_viewModel.GameId, isHunter: false);
            await _hudViewModel.ActivateAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.MapChanged -= OnMapChanged;
        _camera.FollowModeChanged -= OnFollowModeChanged;
        // Deactivate the HUD (unsubscribe) before the map VM stops the shared store connection.
        _hudViewModel.Deactivate();
        _viewModel.Deactivate();
    }

    private void OnMapChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(RedrawMap);

    // The HUD's Center toggle flipped. Re-pin straight away rather than waiting for the next position
    // update — those arrive a ping interval apart, so the button would otherwise feel dead for a minute.
    private void OnFollowModeChanged(object? sender, bool following) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (following && _viewModel.SelfPosition is { } fix)
                CenterOn(fix.Latitude, fix.Longitude, resetZoom: !_centeredOnce);
        });

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
        feature.Styles.Add(GameMapPalette.SelfArrowStyle(_accumulatedHeading));
        _selfLayer.Features = [feature];

        // Following: every fix re-pins the camera. Not following: centre only the very first time, so a
        // free-panning player is never yanked back to themselves.
        if (_camera.IsFollowing || !_centeredOnce)
            CenterOn(fix.Latitude, fix.Longitude, resetZoom: !_centeredOnce);
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

    /// <summary>
    /// Centres the camera. <paramref name="resetZoom"/> snaps to the street-level resolution as well —
    /// right for the initial framing, wrong while following, where re-zooming on every fix would undo
    /// whatever zoom the player chose.
    /// </summary>
    private void CenterOn(double latitude, double longitude, bool resetZoom = true)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        if (resetZoom)
            _mapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), StreetLevelResolution);
        else
            _mapControl.Map.Navigator.CenterOn(new MPoint(x, y));
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
