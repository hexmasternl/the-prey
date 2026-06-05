using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Features.StartGame;

public sealed class StartGameCommandHandler : ICommandHandler<StartGameCommand, StartGameResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameMetrics _metrics;
    private readonly IGameEventBus _eventBus;
    private readonly ILobbyEventBus _lobbyEventBus;
    private readonly IGameEngineTrigger _engineTrigger;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StartGameCommandHandler> _logger;

    public StartGameCommandHandler(
        IGameRepository games,
        IGameMetrics metrics,
        IGameEventBus eventBus,
        ILobbyEventBus lobbyEventBus,
        IGameEngineTrigger engineTrigger,
        TimeProvider timeProvider,
        ILogger<StartGameCommandHandler> logger)
    {
        _games = games;
        _metrics = metrics;
        _eventBus = eventBus;
        _lobbyEventBus = lobbyEventBus;
        _engineTrigger = engineTrigger;
        _timeProvider = timeProvider;
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

        using var activity = GameActivitySource.Source.StartActivity("StartGame");
        activity?.SetTag("game.id", game.Id);

        game.Start(command.HunterUserId, _timeProvider.GetUtcNow());

        await _games.UpdateAsync(game, ct);

        await _engineTrigger.TriggerAsync(game.Id, ct);

        _metrics.RecordGameStarted();
        _logger.LogInformation("Game {GameId} started with hunter {HunterId}", game.Id, command.HunterUserId);

        await _eventBus.PublishAsync(game.Id, new StateChangedEvent(game.Id, "InProgress"), ct);
        await _lobbyEventBus.PublishAsync(game.Id, "game-started", game.ToDto(), ct);

        return new StartGameResult(game.ToDto());
    }
}
