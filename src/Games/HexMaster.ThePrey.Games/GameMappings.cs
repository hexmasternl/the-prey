using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;

namespace HexMaster.ThePrey.Games;

internal static class GameMappings
{
    internal static GameStatusDto ToStatusDto(this Game game, PlayfieldInfo? playfieldInfo, Guid userId, DateTimeOffset now)
    {
        var scheduledEndAt = game.ScheduledEndAt;
        var gameDurationLeft = scheduledEndAt.HasValue
            ? (int)Math.Max(0, (scheduledEndAt.Value - now).TotalSeconds)
            : 0;

        var nextPingDuration = game.IsParticipant(userId) ? ComputeNextPingDuration(game, userId, now) : 0;
        var isEndgame = game.IsInFinalStage(now);

        var preys = game.Preys.Select(p => p.ToStatusDto(game, now, isHunter: false)).ToList();
        var preysLeft = preys.Count(p => p.State is "Active" or "Passive");

        return new GameStatusDto(
            game.Id,
            playfieldInfo?.Name ?? string.Empty,
            playfieldInfo?.Coordinates ?? [],
            game.Hunter?.ToStatusDto(game, now, isHunter: true),
            preys,
            gameDurationLeft,
            nextPingDuration,
            isEndgame,
            preysLeft);
    }

    private static int ComputeNextPingDuration(Game game, Guid userId, DateTimeOffset now)
    {
        var interval = game.ReportingIntervalFor(userId, now);
        var participant = game.Hunter?.UserId == userId ? game.Hunter : game.Preys.FirstOrDefault(p => p.UserId == userId);
        var lastLocation = participant?.Locations.OrderByDescending(l => l.RecordedAt).FirstOrDefault();
        if (lastLocation is null) return interval;
        return (int)Math.Max(0, (lastLocation.RecordedAt.AddSeconds(interval) - now).TotalSeconds);
    }

    private static GameParticipantStatusDto ToStatusDto(this GameParticipant participant, Game game, DateTimeOffset now, bool isHunter)
    {
        var callsign = game.Lobby.FirstOrDefault(l => l.UserId == participant.UserId)?.DisplayName ?? "Unknown";
        var location = participant.Location is null
            ? null
            : new GpsCoordinateDto(participant.Location.Latitude, participant.Location.Longitude);
        var state = isHunter ? "Active" : participant.State.ToString();
        return new GameParticipantStatusDto(participant.UserId, callsign, location, participant.HasActivePenalty(now), state);
    }


    /// <summary>
    /// Maps a game to its DTO from <paramref name="currentUserId"/>'s perspective. When the caller is
    /// known, <see cref="GameDto.IsOwnerPlayer"/> reflects whether they own the game; for broadcast
    /// payloads (audience not yet known) pass null and let the SSE endpoint re-stamp it per subscriber.
    /// <see cref="GameDto.IsReadyToStart"/> is caller-independent game state and is always accurate.
    /// </summary>
    internal static GameDto ToDto(this Game game, Guid? currentUserId = null) =>
        new(
            game.Id,
            game.GameCode,
            game.PlayfieldId,
            game.OwnerUserId,
            game.Status.ToString(),
            game.Configuration.ToDto(),
            game.Lobby.Select(p => new LobbyPlayerDto(
                p.UserId,
                p.DisplayName,
                p.ProfilePictureUrl,
                p.IsReady,
                game.DesignatedHunterUserId == p.UserId)).ToList(),
            game.Hunter?.ToDto(),
            game.Preys.Select(p => p.ToDto()).ToList(),
            game.StartedAt,
            game.DesignatedHunterUserId,
            game.CreatedAt,
            game.EndsAt,
            game.CleanUpAfter,
            game.Outcome.ToString(),
            game.CompletedAt,
            currentUserId is { } uid && game.OwnerUserId == uid,
            game.IsReadyToStart);

    internal static GameEndedEvent ToGameEndedEvent(this Game game) =>
        new(
            game.Id,
            game.Outcome.ToString(),
            game.Preys.Count(p => p.State is PlayerState.Active or PlayerState.Passive));

    internal static GameSummaryDto ToSummaryDto(this Game game) =>
        new(game.Id, game.GameCode, game.PlayfieldId, game.OwnerUserId, game.Status.ToString(), game.Lobby.Count);

    internal static GameConfigurationDto ToDto(this GameConfiguration configuration) =>
        new(
            configuration.GameDuration,
            configuration.HunterDelayTime,
            configuration.FinalStageDuration,
            configuration.DefaultLocationInterval,
            configuration.FinalLocationInterval,
            configuration.EnablePreyBoundaryPenalties,
            configuration.EnableHunterBoundaryPenalty);

    internal static ParticipantDto ToDto(this GameParticipant participant) =>
        new(
            participant.UserId,
            participant.Role.ToString(),
            participant.Location is { } location ? new GpsCoordinateDto(location.Latitude, location.Longitude) : null,
            participant.Penalties.Select(p => new PenaltyDto(p.Id, p.EndsAt)).ToList(),
            participant.Locations
                .Select(l => new LocationReadingDto(l.Id, new GpsCoordinateDto(l.Coordinate.Latitude, l.Coordinate.Longitude), l.RecordedAt))
                .ToList());
}
