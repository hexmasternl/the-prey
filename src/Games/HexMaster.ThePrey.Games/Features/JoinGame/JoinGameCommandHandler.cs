using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.Games.Features.JoinGame;

public sealed class JoinGameCommandHandler : ICommandHandler<JoinGameCommand, JoinGameResult?>
{
    private readonly IGameRepository _games;

    public JoinGameCommandHandler(IGameRepository games) => _games = games;

    public async Task<JoinGameResult?> Handle(JoinGameCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _games.GetByIdAsync(command.GameId, ct);
        if (game is null)
            return null;

        game.JoinLobby(LobbyPlayer.Create(command.UserId, command.DisplayName, command.ProfilePictureUrl));

        await _games.UpdateAsync(game, ct);

        return new JoinGameResult(game.ToDto());
    }
}
