using System.Diagnostics;
using System.Security.Cryptography;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Features.CreateGame;

public sealed class CreateGameCommandHandler : ICommandHandler<CreateGameCommand, CreateGameResult>
{
    /// <summary>How often a colliding game code is regenerated before giving up.</summary>
    public const int MaxGameCodeAttempts = 5;

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

            var creator = LobbyPlayer.Create(command.OwnerUserId, command.DisplayName, command.ProfilePictureUrl);

            var game = await PersistWithUniqueCodeAsync(command, configuration, creator, activity, ct);

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

    /// <summary>
    /// Persists the game under a freshly generated code, regenerating on a code collision
    /// up to <see cref="MaxGameCodeAttempts"/> times.
    /// </summary>
    private async Task<Game> PersistWithUniqueCodeAsync(
        CreateGameCommand command,
        GameConfiguration configuration,
        LobbyPlayer creator,
        Activity? activity,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var game = Game.Create(command.OwnerUserId, command.PlayfieldId, GenerateGameCode(), configuration);
            game.JoinLobby(creator);

            try
            {
                await _games.AddAsync(game, ct);
                activity?.SetTag("game.code_attempts", attempt);
                return game;
            }
            catch (DuplicateGameCodeException) when (attempt < MaxGameCodeAttempts)
            {
                _logger.LogWarning(
                    "Game code collision on attempt {Attempt} of {MaxAttempts}; regenerating",
                    attempt, MaxGameCodeAttempts);
            }
        }
    }

    /// <summary>A cryptographically random code of exactly <see cref="Game.GameCodeLength"/> decimal digits.</summary>
    private static string GenerateGameCode() =>
        RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8");
}
