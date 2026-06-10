using Bogus;
using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.Games.Tests.Factories;

internal static class GameFaker
{
    private static readonly Faker _faker = new();

    internal static GameConfiguration ValidConfiguration(
        int gameDuration = 60,
        int hunterDelayTime = 5,
        int finalStageDuration = 10,
        int defaultLocationInterval = 30,
        int finalLocationInterval = 10,
        bool enablePreyBoundaryPenalties = false,
        bool enableHunterBoundaryPenalty = false) =>
        GameConfiguration.Create(
            gameDuration,
            hunterDelayTime,
            finalStageDuration,
            defaultLocationInterval,
            finalLocationInterval,
            enablePreyBoundaryPenalties,
            enableHunterBoundaryPenalty);

    internal static LobbyPlayer Player(Guid? userId = null, string? displayName = null, string? profilePictureUrl = null) =>
        LobbyPlayer.Create(userId ?? Guid.NewGuid(), displayName ?? _faker.Name.FullName(), profilePictureUrl);

    internal static string ValidGameCode() =>
        _faker.Random.Int(0, (int)Math.Pow(10, Game.GameCodeLength) - 1).ToString("D" + Game.GameCodeLength);

    internal static Game LobbyGame(
        Guid? ownerId = null,
        Guid? playfieldId = null,
        string? gameCode = null,
        GameConfiguration? configuration = null) =>
        Game.Create(ownerId ?? Guid.NewGuid(), playfieldId ?? Guid.NewGuid(), gameCode ?? ValidGameCode(), configuration ?? ValidConfiguration());

    /// <summary>
    /// A lobby game pre-filled with <paramref name="playerCount"/> players; returns their ids in join order.
    /// By default every player is readied up so the game satisfies <see cref="Game.Start"/>'s readiness gate;
    /// pass <paramref name="markReady"/> = false to leave them un-readied (e.g. to test that gate).
    /// </summary>
    internal static Game LobbyGameWithPlayers(int playerCount, out IReadOnlyList<Guid> playerIds, GameConfiguration? configuration = null, bool markReady = true)
    {
        var game = LobbyGame(configuration: configuration);
        var ids = new List<Guid>();
        for (var i = 0; i < playerCount; i++)
        {
            var id = Guid.NewGuid();
            game.JoinLobby(Player(id));
            if (markReady)
                game.SetReady(id);
            ids.Add(id);
        }

        playerIds = ids;
        return game;
    }

    /// <summary>A started game; the first player becomes the hunter, the rest preys.</summary>
    internal static Game StartedGame(
        out Guid hunterId,
        out IReadOnlyList<Guid> preyIds,
        DateTimeOffset startedAt,
        int playerCount = 3,
        GameConfiguration? configuration = null)
    {
        var game = LobbyGameWithPlayers(playerCount, out var ids, configuration);
        hunterId = ids[0];
        preyIds = ids.Skip(1).ToList();
        game.Start(hunterId, startedAt);
        return game;
    }
}
