using MauiColor = Microsoft.Maui.Graphics.Color;
using MapsuiColor = Mapsui.Styles.Color;

namespace HexMaster.ThePrey.Maui.App.Pages;

/// <summary>
/// The single source of truth for the area-editor's Mapsui feature colours. It derives them from the
/// central <c>Colors.xaml</c> design tokens (<c>TpSignal</c> for the green vertices/polygon, <c>TpHunter</c>
/// for the selected-vertex red), so the map rendering stays in step with the rest of the app rather than
/// hard-coding hex values in the page code-behind. The polygon-fill opacity is the one editor-specific
/// constant.
/// </summary>
internal static class AreaEditorPalette
{
    /// <summary>Fill opacity of the polygon (≈25%), leaving the map legible beneath it.</summary>
    public const double PolygonFillOpacity = 0.25;

    // Fallbacks mirror the Colors.xaml token values, used only if the resource lookup fails.
    private const string SignalFallback = "#64ff00";
    private const string HunterFallback = "#ff2f1f";

    /// <summary>Green dot for an ordinary vertex.</summary>
    public static MapsuiColor Vertex => ToMapsui(Signal());

    /// <summary>Green outline of an ordinary vertex.</summary>
    public static MapsuiColor VertexOutline => ToMapsui(Signal());

    /// <summary>Red outline marking the selected vertex.</summary>
    public static MapsuiColor SelectedOutline => ToMapsui(Hunter());

    /// <summary>Green outline of the polygon.</summary>
    public static MapsuiColor PolygonOutline => ToMapsui(Signal());

    /// <summary>Green, ~25%-opacity polygon fill.</summary>
    public static MapsuiColor PolygonFill => ToMapsui(Signal(), PolygonFillOpacity);

    private static MauiColor Signal() => Resolve("TpSignal", SignalFallback);

    private static MauiColor Hunter() => Resolve("TpHunter", HunterFallback);

    private static MauiColor Resolve(string tokenKey, string fallbackHex) =>
        Application.Current?.Resources.TryGetValue(tokenKey, out var value) == true && value is MauiColor color
            ? color
            : MauiColor.FromArgb(fallbackHex);

    private static MapsuiColor ToMapsui(MauiColor color, double alpha = 1.0) => new(
        (int)Math.Round(color.Red * 255),
        (int)Math.Round(color.Green * 255),
        (int)Math.Round(color.Blue * 255),
        (int)Math.Round(color.Alpha * alpha * 255));
}
