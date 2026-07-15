using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using NetTopologySuite.Geometries;
// Mapsui.Styles.Brush/Pen alias to Mapsui's own types — Microsoft.Maui.Controls (in scope via the
// MAUI SDK's implicit global usings) also defines a Brush class, which would otherwise be ambiguous.
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiPen = Mapsui.Styles.Pen;
// NetTopologySuite.Geometries.Polygon alias — Mapsui.UI.Maui (the MapControl namespace, brought in by
// the "using Mapsui.UI.Maui;" above) also declares its own legacy Polygon drawable, which would
// otherwise be ambiguous with the NTS geometry type used to build the area's fill/outline.
using NtsPolygon = NetTopologySuite.Geometries.Polygon;

namespace HexMaster.ThePrey.Maui.App.Pages;

/// <summary>
/// Interactive map "area editor". Hosts a Mapsui <see cref="MapControl"/> (created here rather than in
/// XAML — see <c>DefineAreaPage.xaml</c>) over an OpenStreetMap tile layer, with two feature layers drawn
/// on top: the in-progress polygon and its vertices. All polygon rules (add/select/move/delete, save
/// gating) live in <see cref="DefineAreaViewModel"/>; this page only translates Mapsui gestures into VM
/// calls and re-renders the two feature layers whenever <see cref="DefineAreaViewModel.Changed"/> fires.
/// </summary>
public partial class DefineAreaPage : ContentPage
{
    /// <summary>Fallback map center (Amsterdam) used when no GPS fix is available and there is no seed.</summary>
    private const double DefaultLatitude = 52.3676;
    private const double DefaultLongitude = 4.9041;

    /// <summary>
    /// Street-level resolution (meters/pixel) for the initial center, derived from the standard Web
    /// Mercator formula <c>156543.03392804097 / 2^zoom</c> at zoom 17.
    /// </summary>
    private static readonly double StreetLevelResolution = 156543.03392804097 / Math.Pow(2, 17);

    /// <summary>Touch tolerance (device-independent pixels) for hitting a vertex marker with a finger.</summary>
    private const double VertexHitRadius = 24;

    private const double VertexSymbolScale = 0.35;
    private const double VertexOutlineWidth = 2;
    private const double SelectedVertexOutlineWidth = 3;
    private const double PolygonOutlineWidth = 2;

    private readonly DefineAreaViewModel _viewModel;
    private readonly IGpsReader _gpsReader;
    private readonly IAreaEditorNavigator _navigator;
    private readonly MapControl _mapControl;

    /// <summary>Polygon fill/outline, redrawn from <see cref="DefineAreaViewModel.Vertices"/>.</summary>
    private readonly MemoryLayer _polygonLayer = new("Area");

    /// <summary>One point feature per vertex, redrawn from <see cref="DefineAreaViewModel.Vertices"/>.</summary>
    private readonly MemoryLayer _vertexLayer = new("Vertices");

    /// <summary>Index of the vertex currently being dragged, or <c>null</c> when not dragging.</summary>
    private int? _dragIndex;

    public DefineAreaPage(DefineAreaViewModel viewModel, IGpsReader gpsReader, IAreaEditorNavigator navigator)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _gpsReader = gpsReader;
        _navigator = navigator;
        BindingContext = viewModel;

        _mapControl = new MapControl
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
        _mapControl.Map.Layers.Add(_polygonLayer);
        _mapControl.Map.Layers.Add(_vertexLayer);

        _mapControl.MapTapped += OnMapTapped;
        _mapControl.MapPointerPressed += OnMapPointerPressed;
        _mapControl.MapPointerMoved += OnMapPointerMoved;
        _mapControl.MapPointerReleased += OnMapPointerReleased;

        MapHost.Children.Add(_mapControl);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Subscribed before Seed so the very first draw goes through the normal Changed → redraw path.
        _viewModel.Changed += OnViewModelChanged;
        _viewModel.Seed(_navigator.Seed);

        await CenterMapAsync();
        RedrawLayers();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.Changed -= OnViewModelChanged;
        _mapControl.MapTapped -= OnMapTapped;
        _mapControl.MapPointerPressed -= OnMapPointerPressed;
        _mapControl.MapPointerMoved -= OnMapPointerMoved;
        _mapControl.MapPointerReleased -= OnMapPointerReleased;
    }

    private void OnTrashClicked(object? sender, EventArgs e) => _viewModel.DeleteSelected();

    /// <summary>
    /// Centers on the polygon centroid when re-editing an existing polygon; otherwise tries a live GPS
    /// fix and falls back to a fixed default center. Never throws — a missing fix is an expected, not
    /// exceptional, outcome, but <see cref="IGpsReader.ReadAsync"/> is guarded anyway so a misbehaving
    /// platform implementation can't take the editor down with it.
    /// </summary>
    private async Task CenterMapAsync()
    {
        // Re-editing an existing polygon: frame it by centring on its centroid rather than the
        // current location (create starts empty and falls through to the GPS fix below).
        if (_viewModel.Centroid is { } centroid)
        {
            CenterOn(centroid.Latitude, centroid.Longitude);
            return;
        }

        GpsFix? fix = null;
        try
        {
            fix = await _gpsReader.ReadAsync();
        }
        catch
        {
            // No fix available — fall through to the default center below.
        }

        CenterOn(fix?.Latitude ?? DefaultLatitude, fix?.Longitude ?? DefaultLongitude);
    }

    private void CenterOn(double latitude, double longitude)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);

        // CenterOnAndZoomTo queues itself internally (via Navigator's postponed-calls list) until the
        // control has been measured, so it's safe to call before the first layout pass has run.
        _mapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), StreetLevelResolution);
    }

    private void OnViewModelChanged(object? sender, EventArgs e) => RedrawLayers();

    /// <summary>Rebuilds both feature layers from the view model's current vertex/selection state.</summary>
    private void RedrawLayers()
    {
        var vertices = _viewModel.Vertices;
        var selectedIndex = _viewModel.SelectedIndex;

        var vertexFeatures = new IFeature[vertices.Count];
        for (var i = 0; i < vertices.Count; i++)
            vertexFeatures[i] = CreateVertexFeature(vertices[i], i == selectedIndex);
        _vertexLayer.Features = vertexFeatures;

        _polygonLayer.Features = vertices.Count >= DefineAreaViewModel.MinPoints
            ? [CreatePolygonFeature(vertices)]
            : [];

        // The layers' data didn't come from a fetch, so a plain graphics refresh (no re-fetch) is enough.
        _mapControl.RefreshGraphics();
    }

    private static PointFeature CreateVertexFeature(GpsCoordinate vertex, bool isSelected)
    {
        var (x, y) = SphericalMercator.FromLonLat(vertex.Longitude, vertex.Latitude);
        var feature = new PointFeature(x, y);
        feature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = VertexSymbolScale,
            Fill = new MapsuiBrush(AreaEditorPalette.Vertex),
            Outline = new MapsuiPen(
                isSelected ? AreaEditorPalette.SelectedOutline : AreaEditorPalette.VertexOutline,
                isSelected ? SelectedVertexOutlineWidth : VertexOutlineWidth)
        });
        return feature;
    }

    /// <summary>
    /// Builds the polygon as an NTS <see cref="NtsPolygon"/> wrapped in a Mapsui.Nts <see cref="GeometryFeature"/>
    /// — Mapsui's core package has no polygon geometry of its own, only points (see <see cref="PointFeature"/>).
    /// </summary>
    private static GeometryFeature CreatePolygonFeature(IReadOnlyList<GpsCoordinate> vertices)
    {
        var ring = new Coordinate[vertices.Count + 1];
        for (var i = 0; i < vertices.Count; i++)
        {
            var (x, y) = SphericalMercator.FromLonLat(vertices[i].Longitude, vertices[i].Latitude);
            ring[i] = new Coordinate(x, y);
        }
        ring[^1] = ring[0]; // Close the ring — NTS requires the first and last coordinates to match.

        var feature = new GeometryFeature(new NtsPolygon(new LinearRing(ring)));
        feature.Styles.Add(new VectorStyle
        {
            Fill = new MapsuiBrush(AreaEditorPalette.PolygonFill),
            Outline = new MapsuiPen(AreaEditorPalette.PolygonOutline, PolygonOutlineWidth)
        });
        return feature;
    }

    /// <summary>Single tap: select a hit vertex, or add a new one at the tapped location.</summary>
    private void OnMapTapped(object? sender, MapEventArgs e)
    {
        if (e.GestureType != GestureType.SingleTap)
            return;

        if (HitTestVertex(e.ScreenPosition) is int index)
        {
            _viewModel.SelectVertex(index);
        }
        else
        {
            var (lon, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
            _viewModel.AddVertex(lat, lon);
        }

        e.Handled = true;
    }

    /// <summary>Remembers whether the press landed on a vertex, so a following move can drag it.</summary>
    private void OnMapPointerPressed(object? sender, MapEventArgs e)
    {
        _dragIndex = HitTestVertex(e.ScreenPosition);
    }

    /// <summary>
    /// Drags the vertex the press started on. Marking the event handled suppresses the MapControl's own
    /// pan/zoom manipulation for this gesture, so the map doesn't scroll underneath the dragged vertex;
    /// an empty-area drag never sets <see cref="_dragIndex"/> and so falls through to normal panning.
    /// </summary>
    private void OnMapPointerMoved(object? sender, MapEventArgs e)
    {
        if (_dragIndex is not int index)
            return;

        if (_viewModel.SelectedIndex != index)
            _viewModel.SelectVertex(index);

        var (lon, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
        _viewModel.MoveSelected(lat, lon);

        e.Handled = true;
    }

    private void OnMapPointerReleased(object? sender, MapEventArgs e)
    {
        _dragIndex = null;
    }

    /// <summary>
    /// Finds the topmost vertex within <see cref="VertexHitRadius"/> device-independent pixels of a
    /// screen position, searching from the last vertex back so a visually overlapping later vertex wins.
    /// </summary>
    private int? HitTestVertex(ScreenPosition screenPosition)
    {
        var viewport = _mapControl.Map.Navigator.Viewport;
        var vertices = _viewModel.Vertices;

        for (var i = vertices.Count - 1; i >= 0; i--)
        {
            var (x, y) = SphericalMercator.FromLonLat(vertices[i].Longitude, vertices[i].Latitude);
            var vertexScreenPosition = viewport.WorldToScreen(new MPoint(x, y));
            if (vertexScreenPosition.Distance(screenPosition) <= VertexHitRadius)
                return i;
        }

        return null;
    }
}
