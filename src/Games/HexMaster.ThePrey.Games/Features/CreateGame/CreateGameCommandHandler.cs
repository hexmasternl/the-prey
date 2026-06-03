using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Features.CreateGame;

public sealed class CreateGameCommandHandler : ICommandHandler<CreateGameCommand, CreateGameResult>
{
    private readonly IGameRepository _games;
    private readonly IGameMetrics _metrics;
    private readonly ILogger<CreateGameCommandHandler> _logger;

    public CreateGameCommandHandler(
        IGameRepository games,
        IGameMetrics metrics,
        ILogger<CreateGameCommandHandler> logger)
    {
        _games = games;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<CreateGameResult> Handle(CreateGameCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("CreateGame");
        activity?.SetTag("game.owner_id", command.OwnerUserId);

        try
        {
            var configuration = GameConfiguration.Create(
                command.GameDuration,
                command.HunterDelayTime,
                command.FinalStageDuration,
                command.DefaultLocationInterval,
                command.FinalLocationInterval,
                command.EnablePreyBoundaryPenalties,
                command.EnableHunterBoundaryPenalty);

            var game = Game.Create(command.OwnerUserId, command.PlayfieldId, configuration);

            await _games.AddAsync(game, ct);

            _metrics.RecordGameCreated();
            _logger.LogInformation("Game {GameId} created for owner {OwnerId}", game.Id, command.OwnerUserId);

            activity?.SetTag("game.id", game.Id);

            return new CreateGameResult(game.ToDto());
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
