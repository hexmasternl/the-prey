using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.RemoveLobbyPlayer;

public sealed class RemoveLobbyPlayerCommandHandler : ICommandHandler<RemoveLobbyPlayerCommand, RemoveLobbyPlayerResult?>
{
    private readonly IGameRepository _games;
    private readonly ILobbyEventBus _eventBus;

    public RemoveLobbyPlayerCommandHandler(IGameRepository games, ILobbyEventBus eventBus)
    {
        _games = games;
        _eventBus = eventBus;
    }

    public async Task<RemoveLobbyPlayerResult?> Handle(RemoveLobbyPlayerCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("RemoveLobbyPlayer");
        activity?.SetTag("game.id", command.GameId);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct);
            if (game is null)
                return null;

            if (game.OwnerUserId != command.OwnerUserId)
                throw new InvalidOperationException("Only the game owner can remove players.");

            game.RemoveLobbyPlayer(command.TargetUserId);

            await _games.UpdateAsync(game, ct);
            await _eventBus.PublishAsync(game.Id, "lobby-updated", game.ToDto(), ct);

            return new RemoveLobbyPlayerResult(game.ToDto(command.OwnerUserId));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
