using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.SetReady;

public sealed class SetReadyCommandHandler : ICommandHandler<SetReadyCommand, SetReadyResult?>
{
    private readonly IGameRepository _games;
    private readonly ILobbyEventBus _eventBus;

    public SetReadyCommandHandler(IGameRepository games, ILobbyEventBus eventBus)
    {
        _games = games;
        _eventBus = eventBus;
    }

    public async Task<SetReadyResult?> Handle(SetReadyCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("SetReady");
        activity?.SetTag("game.id", command.GameId);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct);
            if (game is null)
                return null;

            if (!game.IsVisibleTo(command.UserId))
                throw new InvalidOperationException("This player is not in the lobby.");

            game.SetReady(command.UserId);

            await _games.UpdateAsync(game, ct);
            await _eventBus.PublishAsync(game.Id, "ready-updated", game.ToDto(), ct);

            return new SetReadyResult(game.ToDto(command.UserId));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
