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

        var isParticipant = game.IsParticipant(userId);
        var nextPingDuration = isParticipant ? ComputeNextPingDuration(game, userId, now) : 0;
        var nextPingDurationWithPenalty = isParticipant ? ComputeNextPingDurationWithPenalty(game, userId, now) : 0;
        var currentPingInterval = isParticipant ? ComputeCurrentPingInterval(game, userId, now) : 0;
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
            nextPingDurationWithPenalty,
            currentPingInterval,
            isEndgame,
            preysLeft,
            game.HunterMayMoveAt);
    }

    /// <summary>
    /// Computes the whole seconds left until the next update for the given participant, using a hybrid rule:
    /// <list type="bullet">
    ///   <item><b>Penalised participant</b> — personal cadence anchored to their most recent recorded location:
    ///         <c>lastLocation.RecordedAt + penaltyInterval - now</c>. Falls back to the full interval when
    ///         no location has been recorded yet. Clamped to <c>[0, interval]</c>.</item>
    ///   <item><b>Non-penalised participant</b> — game-wide synced cadence: starting from
    ///         <see cref="Game.NextScheduledBroadcastOn"/>, advance by <paramref name="interval"/> seconds
    ///         until the scheduled moment is in the future, then return the seconds remaining. Falls back to
    ///         <paramref name="interval"/> when <see cref="Game.NextScheduledBroadcastOn"/> is null.
    ///         Clamped to <c>[0, interval]</c>.</item>
    /// </list>
    /// </summary>
    private static int ComputeNextPingDuration(Game game, Guid userId, DateTimeOffset now)
    {
        var interval = game.ReportingIntervalFor(userId, now);
        var participant = game.Participants.FirstOrDefault(p => p.UserId == userId);

        if (participant is not null && participant.HasActivePenalty(now))
        {
            var lastLocation = participant.Locations.OrderByDescending(l => l.RecordedAt).FirstOrDefault();
            if (lastLocation is null)
                return interval;
            var secondsLeft = (lastLocation.RecordedAt.AddSeconds(interval) - now).TotalSeconds;
            return (int)Math.Max(0, Math.Min(interval, secondsLeft));
        }

        if (game.NextScheduledBroadcastOn is null)
            return interval;

        var scheduled = game.NextScheduledBroadcastOn.Value;
        while (scheduled <= now)
            scheduled = scheduled.AddSeconds(interval);

        var remaining = (scheduled - now).TotalSeconds;
        return (int)Math.Max(0, Math.Min(interval, remaining));
    }

    private static int ComputeCurrentPingInterval(Game game, Guid userId, DateTimeOffset now) =>
        game.ReportingIntervalFor(userId, now);

    /// <summary>
    /// Computes the whole seconds remaining until the next sweep tick for a penalised participant,
    /// clamped to [0, <see cref="Game.SweepIntervalSeconds"/>]. Returns 0 for non-penalised
    /// participants. The client seeds its fixed-30-second penalty countdown bar from this value.
    /// </summary>
    private static int ComputeNextPingDurationWithPenalty(Game game, Guid userId, DateTimeOffset now)
    {
        var participant = game.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is null || !participant.HasActivePenalty(now))
            return 0;

        if (game.LastSweptOn is null)
            return Game.SweepIntervalSeconds;

        var secondsLeft = (game.LastSweptOn.Value.AddSeconds(Game.SweepIntervalSeconds) - now).TotalSeconds;
        return (int)Math.Max(0, Math.Min(Game.SweepIntervalSeconds, secondsLeft));
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

    internal static GameExportDto ToExportDto(this Game game) =>
        new(
            game.Id,
            game.GameCode,
            game.PlayfieldId,
            game.OwnerUserId,
            game.Status.ToString(),
            game.Configuration.ToDto(),
            game.StartedAt,
            game.CreatedAt,
            game.EndsAt,
            game.CleanUpAfter,
            game.CompletedAt,
            game.Outcome.ToString(),
            game.HunterUserId,
            game.Preys,
            game.Participants.Select(p => p.ToExportDto(game.HunterUserId)).ToList());

    private static ParticipantExportDto ToExportDto(this GameParticipant participant, Guid? hunterUserId) =>
        new(
            participant.UserId,
            participant.DisplayName,
            participant.ProfilePictureUrl,
            participant.IsReady,
            participant.State.ToString(),
            participant.LastLocationAt,
            participant.Location is { } loc ? new GpsCoordinateDto(loc.Latitude, loc.Longitude) : null,
            participant.DelayAnchorLocation is { } anchor ? new GpsCoordinateDto(anchor.Latitude, anchor.Longitude) : null,
            participant.DelayPenaltyApplied,
            participant.UserId == hunterUserId,
            participant.Penalties.Select(p => new PenaltyExportDto(p.Id, p.EndsAt)).ToList(),
            participant.Locations.Select(l => new LocationReadingExportDto(l.Id, new GpsCoordinateDto(l.Coordinate.Latitude, l.Coordinate.Longitude), l.RecordedAt, l.Checked)).ToList());

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
