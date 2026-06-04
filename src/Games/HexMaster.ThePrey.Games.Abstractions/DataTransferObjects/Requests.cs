namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>Create a game. The owner is taken from the authenticated caller.</summary>
public sealed record CreateGameRequest(
    Guid PlayfieldId,
    int GameDuration,
    int HunterDelayTime,
    int FinalStageDuration,
    int DefaultLocationInterval,
    int FinalLocationInterval,
    bool EnablePreyBoundaryPenalties = false,
    bool EnableHunterBoundaryPenalty = false);

/// <summary>Join a game's lobby. The user id is taken from the authenticated caller.</summary>
public sealed record JoinGameRequest(string DisplayName, string? ProfilePictureUrl = null);

/// <summary>Start a game, designating the lobby member who becomes the hunter.</summary>
public sealed record StartGameRequest(Guid HunterUserId);

/// <summary>Report a GPS location. The user id is taken from the authenticated caller.</summary>
public sealed record RecordLocationRequest(
    double Latitude,
    double Longitude,
    DateTimeOffset? RecordedAt = null,
    double? Accuracy = null);
