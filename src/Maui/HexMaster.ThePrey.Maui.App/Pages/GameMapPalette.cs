using HexMaster.ThePrey.Maui.App.ViewModels;
using Mapsui.Styles;
using MauiColor = Microsoft.Maui.Graphics.Color;
using MapsuiColor = Mapsui.Styles.Color;
using MapsuiImage = Mapsui.Styles.Image;   // disambiguates from Microsoft.Maui.Controls.Image

namespace HexMaster.ThePrey.Maui.App.Pages;

/// <summary>
/// The single source of truth for the gameplay maps' Mapsui feature colours, derived from the central
/// <c>Colors.xaml</c> design tokens (<c>TpSignal</c> green, <c>TpHunter</c> red, <c>TpCaught</c> grey) so
/// the map rendering stays in step with the rest of the app. Shared by the hunter and prey pages: the
/// hunter draws a red playfield with red prey dots; the prey draws a green playfield with green other-prey
/// dots and a red hunter dot; both grey out caught preys and draw a green self arrow.
/// </summary>
internal static class GameMapPalette
{
    /// <summary>Fill opacity of the playfield polygon (≈18%), leaving the map legible beneath it.</summary>
    public const double PolygonFillOpacity = 0.18;

    private const string SignalFallback = "#64ff00";
    private const string HunterFallback = "#ff2f1f";
    private const string CaughtFallback = "#6b7267";

    /// <summary>Signal green — the self arrow and (on the prey map) other-prey dots + playfield.</summary>
    public static MapsuiColor Signal => ToMapsui(Resolve("TpSignal", SignalFallback));

    /// <summary>
    /// A slim navigation arrow (SVG, tip pointing up at a 0° heading) for the player's own position
    /// marker. Deliberately far narrower than Mapsui's broad built-in <see cref="SymbolType.Triangle"/>
    /// so the compass heading it is rotated to reads unambiguously; the notched tail gives it the
    /// classic "you are here" look. Colours are overridden at render time (see <see cref="SelfArrowStyle"/>).
    /// </summary>
    private const string SelfArrowSvg =
        "svg-content://<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 34\" width=\"24\" height=\"34\">" +
        "<path d=\"M12 1 L21 33 L12 26 L3 33 Z\" stroke-linejoin=\"round\" stroke-width=\"1\" /></svg>";

    /// <summary>
    /// The signal-green self arrow style, rotated to <paramref name="rotationDegrees"/> (compass heading).
    /// Shared by the hunter and prey maps so both draw an identical, clearly-directional self marker.
    /// </summary>
    public static ImageStyle SelfArrowStyle(double rotationDegrees) => new()
    {
        Image = new MapsuiImage
        {
            Source = SelfArrowSvg,
            SvgFillColor = Signal,
            SvgStrokeColor = Signal,
        },
        SymbolScale = 1.0,
        SymbolRotation = rotationDegrees,
    };

    /// <summary>Hunter red — the hunter dot / prey dots on the hunter map + the hunter playfield.</summary>
    public static MapsuiColor Hunter => ToMapsui(Resolve("TpHunter", HunterFallback));

    /// <summary>Muted grey — a caught/out prey dot.</summary>
    public static MapsuiColor Caught => ToMapsui(Resolve("TpCaught", CaughtFallback));

    /// <summary>The semi-transparent playfield fill for a given outline colour.</summary>
    public static MapsuiColor PolygonFill(MapsuiColor outline) =>
        new(outline.R, outline.G, outline.B, (int)Math.Round(255 * PolygonFillOpacity));

    /// <summary>Maps a blip role to its dot colour on the hunter map (preys red, caught grey).</summary>
    public static MapsuiColor HunterMapDotColor(MapBlipRole role) => role switch
    {
        MapBlipRole.Caught => Caught,
        _ => Hunter,
    };

    /// <summary>Maps a blip role to its dot colour on the prey map (hunter red, other preys green, caught grey).</summary>
    public static MapsuiColor PreyMapDotColor(MapBlipRole role) => role switch
    {
        MapBlipRole.Hunter => Hunter,
        MapBlipRole.Caught => Caught,
        _ => Signal,
    };

    private static MauiColor Resolve(string tokenKey, string fallbackHex) =>
        Application.Current?.Resources.TryGetValue(tokenKey, out var value) == true && value is MauiColor color
            ? color
            : MauiColor.FromArgb(fallbackHex);

    private static MapsuiColor ToMapsui(MauiColor color) => new(
        (int)Math.Round(color.Red * 255),
        (int)Math.Round(color.Green * 255),
        (int)Math.Round(color.Blue * 255),
        (int)Math.Round(color.Alpha * 255));
}
