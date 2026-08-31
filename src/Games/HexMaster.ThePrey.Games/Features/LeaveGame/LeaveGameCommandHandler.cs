using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games;
using HexMaster.ThePrey.IntegrationEvents;

namespace HexMaster.ThePrey.Games.Features.LeaveGame;

/// <summary>
/// Lets a player leave a game that has not started yet. Leaving is a lobby-only action: once the game
/// is under way (<see cref="GameStatus.Started"/> or <see cref="GameStatus.InProgress"/>) a player is
/// committed to it, and the game ends only on its own terms — the last prey being caught or the
/// duration elapsing, both detected by the sweep. Attempting to leave a started, in-progress, or
/// completed game is rejected with <see cref="InvalidOperationException"/> (surfaced as 409).
/// </summary>
public sealed class LeaveGameCommandHandler : ICommandHandler<LeaveGameCommand, LeaveGameResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameEventBus _eventBus;
    private readonly ILobbyEventBus _lobbyEventBus;
    private readonly TimeProvider _timeProvider;

    public LeaveGameCommandHandler(
        IGameRepository games,
        IGameEventBus eventBus,
        ILobbyEventBus lobbyEventBus,
        TimeProvider timeProvider)
    {
        _games = games;
        _eventBus = eventBus;
        _lobbyEventBus = lobbyEventBus;
        _timeProvider = timeProvider;
    }

    public async Task<LeaveGameResult?> Handle(LeaveGameCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("LeaveGame");
        activity?.SetTag("game.id", command.GameId);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct);
            if (game is null)
                return null;

            activity?.SetTag("game.status", game.Status.ToString());

            // The lobby is open in both Lobby and Ready; a non-owner leaving may drop readiness and
            // revert Ready back to Lobby (handled by the aggregate's recompute inside RemoveLobbyPlayer).
            if (game.Status is not (GameStatus.Lobby or GameStatus.Ready))
            {
                activity?.SetTag("game.leave.rejected", true);
                throw new InvalidOperationException(
                    $"A player can only leave a game that has not started yet; this game is {game.Status}.");
            }

            if (game.OwnerUserId == command.UserId)
                await CancelAsOwnerAsync(game, ct);
            else
                await RemoveFromLobbyAsync(game, command.UserId, ct);

            return new LeaveGameResult();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    /// <summary>
    /// The owner abandoning their own lobby cancels the game — it never started, so there is nothing to
    /// hand over and leaving an ownerless lobby behind would strand everyone else in it. The outcome is
    /// <c>Cancelled</c>, which is distinct from the two ways a running game ends.
    /// </summary>
    private async Task CancelAsOwnerAsync(Game game, CancellationToken ct)
    {
        game.CancelByOwner(_timeProvider.GetUtcNow());
        await _games.UpdateAsync(game, ct);
        await _eventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.GameEnded, game.ToGameEndedNotificationDto(), ct);
    }

    private async Task RemoveFromLobbyAsync(Game game, Guid userId, CancellationToken ct)
    {
        if (!game.IsParticipant(userId))
            throw new ArgumentException("This user is not in the lobby.", nameof(userId));

        var wasReadyToStart = game.IsReadyToStart;

        game.RemoveLobbyPlayer(userId);
        await _games.UpdateAsync(game, ct);
        await _lobbyEventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantRemoved, new { userId }, ct);

        if (wasReadyToStart != game.IsReadyToStart)
        {
            await _lobbyEventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged, game.ToConfigurationChangedDto(), ct);
        }
    }
}
