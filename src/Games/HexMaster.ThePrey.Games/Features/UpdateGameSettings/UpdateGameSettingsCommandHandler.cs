using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.IntegrationEvents;

namespace HexMaster.ThePrey.Games.Features.UpdateGameSettings;

public sealed class UpdateGameSettingsCommandHandler : ICommandHandler<UpdateGameSettingsCommand, UpdateGameSettingsResult?>
{
    private readonly IGameRepository _games;
    private readonly ILobbyEventBus _eventBus;

    public UpdateGameSettingsCommandHandler(IGameRepository games, ILobbyEventBus eventBus)
    {
        _games = games;
        _eventBus = eventBus;
    }

    public async Task<UpdateGameSettingsResult?> Handle(UpdateGameSettingsCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("UpdateGameSettings");
        activity?.SetTag("game.id", command.GameId);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct);
            if (game is null)
                return null;

            if (game.OwnerUserId != command.OwnerUserId)
                throw new InvalidOperationException("Only the game owner can update settings.");

            var config = GameConfiguration.Create(
                command.GameDuration,
                command.HunterDelayTime,
                command.FinalStageDuration,
                command.DefaultLocationInterval,
                command.FinalLocationInterval,
                command.EnablePreyBoundaryPenalties,
                command.EnableHunterBoundaryPenalty);

            game.UpdateSettings(config);

            await _games.UpdateAsync(game, ct);
            await _eventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged, game.ToConfigurationChangedDto(), ct);

            return new UpdateGameSettingsResult(game.ToDto(command.OwnerUserId));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
