namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Client projection of the backend <c>RecordLocationRequest</c> — the body POSTed to
/// <c>POST /games/{id}/locations</c>. Serialized with the default web (camelCase) options to match the
/// backend record.
/// </summary>
public sealed record RecordLocationRequest(
    double Latitude,
    double Longitude,
    DateTimeOffset? RecordedAt = null,
    double? Accuracy = null);

/// <summary>
/// Client projection of the backend <c>RecordLocationResponse</c>. <see cref="NextLocationIntervalSeconds"/>
/// is the regular reporting cadence; while a penalty is active <see cref="PenaltyIntervalSeconds"/>
/// overrides it until <see cref="PenaltyEndsAt"/> (both null when no penalty applies). The server owns
/// the ping interval — the coordinator adopts it for the next tick.
/// </summary>
public sealed record RecordLocationResponse(
    bool Accepted,
    int NextLocationIntervalSeconds,
    int? PenaltyIntervalSeconds = null,
    DateTimeOffset? PenaltyEndsAt = null);
