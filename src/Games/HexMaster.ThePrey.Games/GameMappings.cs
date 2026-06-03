using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.Games;

internal static class GameMappings
{
    internal static GameDto ToDto(this Game game) =>
        new(
            game.Id,
            game.PlayfieldId,
            game.OwnerUserId,
            game.Status.ToString(),
            game.Configuration.ToDto(),
            game.Lobby.Select(p => p.ToDto()).ToList(),
            game.Hunter?.ToDto(),
            game.Preys.Select(p => p.ToDto()).ToList(),
            game.StartedAt);

    internal static GameSummaryDto ToSummaryDto(this Game game) =>
        new(game.Id, game.PlayfieldId, game.OwnerUserId, game.Status.ToString(), game.Lobby.Count);

    internal static GameConfigurationDto ToDto(this GameConfiguration configuration) =>
        new(
            configuration.GameDuration,
            configuration.HunterDelayTime,
            configuration.FinalStageDuration,
            configuration.DefaultLocationInterval,
            configuration.FinalLocationInterval,
            configuration.EnablePreyBoundaryPenalties,
            configuration.EnableHunterBoundaryPenalty);

    internal static LobbyPlayerDto ToDto(this LobbyPlayer player) =>
        new(player.UserId, player.DisplayName, player.ProfilePictureUrl);

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
