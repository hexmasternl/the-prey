using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.GetGameState;

public sealed class GetGameStateQueryHandler : IQueryHandler<GetGameStateQuery, GameStateDto?>
{
    private static readonly IReadOnlyList<GpsCoordinateDto> NoLocations = [];

    private readonly IGameRepository _games;

    public GetGameStateQueryHandler(IGameRepository games) => _games = games;

    public async Task<GameStateDto?> Handle(GetGameStateQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var game = await _games.GetByIdAsync(query.GameId, ct);
        if (game is null || game.Status != GameStatus.InProgress)
            return null;

        using var activity = GameActivitySource.Source.StartActivity("GetGameState");
        activity?.SetTag("game.id", game.Id);
        activity?.SetTag("game.user_id", query.UserId);

        var hunter = game.HunterUserId is { } hid
            ? game.Participants.FirstOrDefault(p => p.UserId == hid)
            : null;

        if (hunter?.UserId == query.UserId)
        {
            var preyLocations = game.Participants
                .Where(p => p.UserId != game.HunterUserId && p.Location is not null)
                .Select(p => new GpsCoordinateDto(p.Location!.Latitude, p.Location.Longitude))
                .ToList();

            return new GameStateDto(null, preyLocations);
        }

        var prey = game.Participants.FirstOrDefault(p => p.UserId == query.UserId && p.UserId != game.HunterUserId);
        if (prey is null)
            return null;

        var hunterDistance = hunter?.Location is { } hunterLocation && prey.Location is { } preyLocation
            ? (int?)Math.Round(preyLocation.DistanceInMetersTo(hunterLocation))
            : null;

        return new GameStateDto(hunterDistance, NoLocations);
    }
}
