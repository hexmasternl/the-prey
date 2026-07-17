using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.IntegrationEvents;

namespace HexMaster.ThePrey.Games.Features.JoinGame;

public sealed class JoinGameCommandHandler : ICommandHandler<JoinGameCommand, JoinGameResult?>
{
    private readonly IGameRepository _games;
    private readonly ILobbyEventBus _eventBus;

    public JoinGameCommandHandler(IGameRepository games, ILobbyEventBus eventBus)
    {
        _games = games;
        _eventBus = eventBus;
    }

    public async Task<JoinGameResult?> Handle(JoinGameCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("JoinGame");
        activity?.SetTag("game.id", command.GameId);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct);
            if (game is null)
                return null;

            if (!string.Equals(command.JoinCode, game.GameCode, StringComparison.Ordinal))
                throw new InvalidJoinCodeException();

            game.JoinLobby(GameParticipant.Create(command.UserId, command.DisplayName, command.ProfilePictureUrl));

            await _games.UpdateAsync(game, ct);
            await _eventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantJoined, game.ToParticipantDto(command.UserId), ct);

            return new JoinGameResult(game.ToDto(command.UserId));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
