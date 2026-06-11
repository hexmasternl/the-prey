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

        // Build participants list: all participants with computed State ("Active" for hunter).
        var participantStatuses = game.Participants
            .Select(p => p.ToStatusDto(game, now))
            .ToList();

        var preysLeft = game.Participants
            .Where(p => p.UserId != game.HunterUserId)
            .Count(p => p.State is PlayerState.Active or PlayerState.Passive);

        return new GameStatusDto(
            game.Id,
            playfieldInfo?.Name ?? string.Empty,
            playfieldInfo?.Coordinates ?? [],
            game.HunterUserId,
            participantStatuses,
            gameDurationLeft,
            nextPingDuration,
            isEndgame,
            preysLeft,
            game.HunterMayMoveAt);
    }

    private static int ComputeNextPingDuration(Game game, Guid userId, DateTimeOffset now)
    {
        var interval = game.ReportingIntervalFor(userId, now);
        var participant = game.Participants.FirstOrDefault(p => p.UserId == userId);
        var lastLocation = participant?.Locations.OrderByDescending(l => l.RecordedAt).FirstOrDefault();
        if (lastLocation is null) return interval;
        return (int)Math.Max(0, (lastLocation.RecordedAt.AddSeconds(interval) - now).TotalSeconds);
    }

    private static GameParticipantStatusDto ToStatusDto(this GameParticipant participant, Game game, DateTimeOffset now)
    {
        var location = participant.Location is null
            ? null
            : new GpsCoordinateDto(participant.Location.Latitude, participant.Location.Longitude);
        // Hunter's state is always reported as "Active" regardless of internal state.
        var state = participant.UserId == game.HunterUserId ? "Active" : participant.State.ToString();
        return new GameParticipantStatusDto(participant.UserId, participant.DisplayName, location, participant.HasActivePenalty(now), state);
    }

    /// <summary>
    /// Maps a game to its DTO from <paramref name="currentUserId"/>'s perspective.
    /// </summary>
    internal static GameDto ToDto(this Game game, Guid? currentUserId = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new GameDto(
            game.Id,
            game.GameCode,
            game.PlayfieldId,
            game.OwnerUserId,
            game.Status.ToString(),
            game.Configuration.ToDto(),
            game.Participants.Select(p => p.ToDto(now)).ToList(),
            game.HunterUserId,
            game.Preys,
            game.StartedAt,
            game.CreatedAt,
            game.EndsAt,
            game.CleanUpAfter,
            game.Outcome.ToString(),
            game.CompletedAt,
            currentUserId is { } uid && game.OwnerUserId == uid,
            game.IsReadyToStart);
    }

    internal static GameEndedEvent ToGameEndedEvent(this Game game) =>
        new(
            game.Id,
            game.Outcome.ToString(),
            game.Participants.Where(p => p.UserId != game.HunterUserId)
                             .Count(p => p.State is PlayerState.Active or PlayerState.Passive));

    internal static GameSummaryDto ToSummaryDto(this Game game) =>
        new(game.Id, game.GameCode, game.PlayfieldId, game.OwnerUserId, game.Status.ToString(), game.Participants.Count);

    internal static GameConfigurationDto ToDto(this GameConfiguration configuration) =>
        new(
            configuration.GameDuration,
            configuration.HunterDelayTime,
            configuration.FinalStageDuration,
            configuration.DefaultLocationInterval,
            configuration.FinalLocationInterval,
            configuration.EnablePreyBoundaryPenalties,
            configuration.EnableHunterBoundaryPenalty);

    private static ParticipantDto ToDto(this GameParticipant participant, DateTimeOffset now) =>
        new(
            participant.UserId,
            participant.DisplayName,
            participant.ProfilePictureUrl,
            participant.IsReady,
            participant.State.ToString(),
            participant.Location is { } loc ? new GpsCoordinateDto(loc.Latitude, loc.Longitude) : null,
            participant.HasActivePenalty(now));
}
