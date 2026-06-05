using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.UpdateLocationBroadcast;

public sealed class UpdateLocationBroadcastCommandHandler
    : ICommandHandler<UpdateLocationBroadcastCommand, UpdateLocationBroadcastResult>
{
    private readonly IGameRepository _games;
    private readonly IGameEngineEventBus _engineEventBus;

    public UpdateLocationBroadcastCommandHandler(IGameRepository games, IGameEngineEventBus engineEventBus)
    {
        _games = games;
        _engineEventBus = engineEventBus;
    }

    public async Task<UpdateLocationBroadcastResult> Handle(
        UpdateLocationBroadcastCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("UpdateLocationBroadcast");
        activity?.SetTag("game.id", command.GameId);
        activity?.SetTag("game.location_count", command.Locations.Count);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct);

            if (game is null)
                throw new KeyNotFoundException($"Game {command.GameId} not found.");

            if (game.Status != GameStatus.InProgress)
                throw new InvalidOperationException(
                    $"Game {command.GameId} is not in progress (current status: {game.Status}).");

            foreach (var loc in command.Locations)
            {
                if (!game.IsParticipant(loc.UserId)) continue;
                await _engineEventBus.PublishAsync(
                    command.GameId,
                    new EngineLocationEvent(loc.UserId, loc.Latitude, loc.Longitude),
                    ct);
            }

            return new UpdateLocationBroadcastResult(true);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
