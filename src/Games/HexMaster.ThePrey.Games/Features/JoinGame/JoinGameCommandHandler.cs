using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.JoinGame;

public sealed class JoinGameCommandHandler : ICommandHandler<JoinGameCommand, JoinGameResult?>
{
    private readonly IGameRepository _games;

    public JoinGameCommandHandler(IGameRepository games) => _games = games;

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
                throw new InvalidOperationException("The join code is incorrect.");

            game.JoinLobby(LobbyPlayer.Create(command.UserId, command.DisplayName, command.ProfilePictureUrl));

            await _games.UpdateAsync(game, ct);

            return new JoinGameResult(game.ToDto());
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
