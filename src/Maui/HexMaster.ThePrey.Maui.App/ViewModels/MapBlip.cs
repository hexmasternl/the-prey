using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>The semantic role of a player dot on a gameplay map; the page maps it to a color.</summary>
public enum MapBlipRole
{
    /// <summary>The hunter (drawn red on the prey map; never appears on the hunter's own map).</summary>
    Hunter,

    /// <summary>An active/passive prey (red on the hunter map, green on the prey map).</summary>
    Prey,

    /// <summary>A tagged/out prey (grey on both maps).</summary>
    Caught
}

/// <summary>One player dot on a gameplay map: an identity, a position, and a role that picks its color.</summary>
public sealed record MapBlip(Guid Id, double Latitude, double Longitude, MapBlipRole Role);

/// <summary>Shared, pure helpers for projecting backend participant state onto the gameplay maps.</summary>
public static class GameMapProjection
{
    /// <summary>True when a participant's state means "caught" (tagged or out) — a grey dot.</summary>
    public static bool IsCaught(string? state) =>
        string.Equals(state, "Tagged", StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, "Out", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Projects one participant to a <see cref="MapBlip"/> from the hunter's perspective, or <c>null</c>
    /// for no dot: the hunter's own row is skipped (it is the self arrow), and a prey with no broadcast
    /// location is skipped. Remaining preys are <see cref="MapBlipRole.Prey"/> unless caught.
    /// </summary>
    public static MapBlip? ProjectForHunter(Guid userId, Guid? hunterUserId, GpsCoordinate? location, string state)
    {
        if (hunterUserId is { } hunter && userId == hunter)
            return null; // The hunter is the self arrow, never a dot.
        if (location is null)
            return null; // No dot until a location has been broadcast.
        return new MapBlip(userId, location.Latitude, location.Longitude, IsCaught(state) ? MapBlipRole.Caught : MapBlipRole.Prey);
    }

    /// <summary>
    /// Projects one participant to a <see cref="MapBlip"/> from a prey's perspective, or <c>null</c> for
    /// no dot: the caller's own row is skipped (the self arrow), and a participant with no broadcast
    /// location is skipped. The hunter is <see cref="MapBlipRole.Hunter"/>; other preys are
    /// <see cref="MapBlipRole.Prey"/> unless caught.
    /// </summary>
    public static MapBlip? ProjectForPrey(Guid userId, Guid selfUserId, Guid? hunterUserId, GpsCoordinate? location, string state)
    {
        if (userId == selfUserId)
            return null; // Self is the green arrow, never a dot.
        if (location is null)
            return null;
        if (hunterUserId is { } hunter && userId == hunter)
            return new MapBlip(userId, location.Latitude, location.Longitude, MapBlipRole.Hunter);
        return new MapBlip(userId, location.Latitude, location.Longitude, IsCaught(state) ? MapBlipRole.Caught : MapBlipRole.Prey);
    }
}
