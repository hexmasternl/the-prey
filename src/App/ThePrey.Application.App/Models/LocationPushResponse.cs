namespace ThePrey.Application.App.Models;

/// <summary>
/// The server's acknowledgement of a location push (<c>POST /games/{gameId}/locations</c>).
/// <see cref="NextLocationIntervalSeconds"/> is the regular reporting interval; while a penalty is
/// active <see cref="PenaltyIntervalSeconds"/> overrides it until <see cref="PenaltyEndsAt"/>.
/// </summary>
public sealed class LocationPushResponse
{
    public bool Accepted { get; set; }
    public int NextLocationIntervalSeconds { get; set; }
    public int? PenaltyIntervalSeconds { get; set; }
    public DateTimeOffset? PenaltyEndsAt { get; set; }
}
