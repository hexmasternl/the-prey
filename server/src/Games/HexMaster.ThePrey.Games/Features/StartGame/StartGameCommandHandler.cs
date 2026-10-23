using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Features.StartGame;

public sealed class StartGameCommandHandler : ICommandHandler<StartGameCommand, StartGameResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameMetrics _metrics;
    private readonly ILobbyEventBus _lobbyEventBus;
    private readonly ILogger<StartGameCommandHandler> _logger;

    public StartGameCommandHandler(
        IGameRepository games,
        IGameMetrics metrics,
        ILobbyEventBus lobbyEventBus,
        ILogger<StartGameCommandHandler> logger)
    {
        _games = games;
        _metrics = metrics;
        _lobbyEventBus = lobbyEventBus;
        _logger = logger;
    }

    public async Task<StartGameResult?> Handle(StartGameCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _games.GetByIdAsync(command.GameId, ct);
        if (game is null)
            return null;

        if (game.OwnerUserId != command.RequestingUserId)
            throw new InvalidOperationException("Only the owner can start the game.");

        using var activity = GameActivitySource.Source.StartActivity("ArmGame");
        activity?.SetTag("game.id", game.Id);
        activity?.SetTag("game.armed", true);

        game.Arm(command.HunterUserId);

        await _games.UpdateAsync(game, ct);

        // The game is now Started; the always-running sweep (GameTickService) will promote it to
        // InProgress on its next tick, stamping StartedAt at that point.
        _metrics.RecordGameStarted();
        _logger.LogInformation("Game {GameId} armed with hunter {HunterId}", game.Id, command.HunterUserId);

        await _lobbyEventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged, game.ToConfigurationChangedDto(), ct);

        return new StartGameResult(game.ToDto(command.RequestingUserId));
    }
}
