namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>A prey the hunter is currently within range to tag.</summary>
public sealed record TagCandidateDto(
    Guid UserId,
    string Callsign,
    string State,
    double DistanceMeters);

/// <summary>The preys the hunter may tag right now, judged by GPS proximity.</summary>
public sealed record TagCandidatesDto(
    double RangeMeters,
    IReadOnlyList<TagCandidateDto> Candidates);
