namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Client-side projection of the backend <c>TagCandidateDto</c> — a prey the hunter is currently
/// within range to tag. <see cref="UserId"/> is the participant id used for the tag round-trip.
/// Deserializes case-insensitively from the backend's camelCase JSON.
/// </summary>
public sealed record TagCandidate(
    Guid UserId,
    string Callsign,
    double DistanceMeters,
    string State);
