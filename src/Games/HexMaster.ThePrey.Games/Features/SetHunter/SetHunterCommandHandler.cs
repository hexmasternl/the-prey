using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Features.SetHunter;

public sealed class SetHunterCommandHandler : ICommandHandler<SetHunterCommand, SetHunterResult?>
{
    private readonly IGameRepository _games;
    private readonly ILogger<SetHunterCommandHandler> _logger;

    public SetHunterCommandHandler(IGameRepository games, ILogger<SetHunterCommandHandler> logger)
    {
        _games = games;
        _logger = logger;
    }

    public async Task<SetHunterResult?> Handle(SetHunterCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _games.GetByIdAsync(command.GameId, ct);
        if (game is null)
            return null;

        // A 404 (rather than a validation error) keeps the game's existence hidden from
        // non-hunters and callers of games that are not in progress.
        if (game.Status != GameStatus.InProgress)
            return null;

        if (game.Hunter is null || game.Hunter.UserId != command.CallerUserId)
            return null;

        using var activity = GameActivitySource.Source.StartActivity("SetHunter");
        activity?.SetTag("game.id", game.Id);

        game.SetHunter(command.NewHunterUserId);

        await _games.UpdateAsync(game, ct);

        _logger.LogInformation("Game {GameId} hunter changed to {NewHunterId}", game.Id, command.NewHunterUserId);

        return new SetHunterResult(game.ToDto());
    }
}
