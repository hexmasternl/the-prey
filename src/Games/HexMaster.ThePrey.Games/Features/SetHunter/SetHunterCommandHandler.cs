using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Features.SetHunter;

public sealed class SetHunterCommandHandler : ICommandHandler<SetHunterCommand, SetHunterResult?>
{
    private readonly IGameRepository _games;
    private readonly ILobbyEventBus _eventBus;
    private readonly ILogger<SetHunterCommandHandler> _logger;

    public SetHunterCommandHandler(IGameRepository games, ILobbyEventBus eventBus, ILogger<SetHunterCommandHandler> logger)
    {
        _games = games;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<SetHunterResult?> Handle(SetHunterCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _games.GetByIdAsync(command.GameId, ct);
        if (game is null)
            return null;

        using var activity = GameActivitySource.Source.StartActivity("SetHunter");
        activity?.SetTag("game.id", game.Id);

        try
        {
            if (game.Status is GameStatus.Lobby or GameStatus.Ready)
            {
                // Pre-start (Lobby or Ready): owner designates the pre-game hunter. Re-designating while the
                // game is Ready is allowed and may keep it Ready (the aggregate recomputes readiness).
                if (game.OwnerUserId != command.CallerUserId)
                    return null;

                game.DesignateHunter(command.NewHunterUserId);
                await _games.UpdateAsync(game, ct);
                await _eventBus.PublishAsync(game.Id, "hunter-designated", game.ToDto(), ct);

                _logger.LogInformation("Game {GameId} lobby hunter designated to {NewHunterId}", game.Id, command.NewHunterUserId);
            }
            else if (game.Status == GameStatus.InProgress)
            {
                // In-progress: only the current hunter may pass the role
                if (game.HunterUserId is null || game.HunterUserId != command.CallerUserId)
                    return null;

                game.SetHunter(command.NewHunterUserId);
                await _games.UpdateAsync(game, ct);
                await _eventBus.PublishAsync(game.Id, "hunter-changed", game.ToDto(), ct);

                _logger.LogInformation("Game {GameId} hunter changed to {NewHunterId}", game.Id, command.NewHunterUserId);
            }
            else
            {
                return null;
            }

            return new SetHunterResult(game.ToDto(command.CallerUserId));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
