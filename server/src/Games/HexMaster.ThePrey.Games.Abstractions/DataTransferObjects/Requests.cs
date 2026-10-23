namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>
/// Create a game. The owner is taken from the authenticated caller and joins the lobby as its
/// first player under <paramref name="DisplayName"/>.
/// </summary>
public sealed record CreateGameRequest(
    Guid PlayfieldId,
    string DisplayName,
    int GameDuration,
    int HunterDelayTime,
    int FinalStageDuration,
    int DefaultLocationInterval,
    int FinalLocationInterval,
    bool EnablePreyBoundaryPenalties = false,
    bool EnableHunterBoundaryPenalty = false,
    string? ProfilePictureUrl = null);

/// <summary>Join a game's lobby using the 4-digit join code. The user id is taken from the authenticated caller.</summary>
public sealed record JoinGameRequest(string JoinCode, string DisplayName, string? ProfilePictureUrl = null);

/// <summary>Start a game, designating the lobby member who becomes the hunter.</summary>
public sealed record StartGameRequest(Guid HunterUserId);

/// <summary>Reassign the hunter role to an existing prey. Only the current hunter may call this.</summary>
public sealed record SetHunterRequest(Guid NewHunterUserId);

/// <summary>Report a GPS location. The user id is taken from the authenticated caller.</summary>
public sealed record RecordLocationRequest(
    double Latitude,
    double Longitude,
    DateTimeOffset? RecordedAt = null,
    double? Accuracy = null);

/// <summary>Update game configuration settings. Only the game owner may call this.</summary>
public sealed record UpdateGameSettingsRequest(
    int GameDuration,
    int HunterDelayTime,
    int FinalStageDuration,
    int DefaultLocationInterval,
    int FinalLocationInterval,
    bool EnablePreyBoundaryPenalties = false,
    bool EnableHunterBoundaryPenalty = false);
