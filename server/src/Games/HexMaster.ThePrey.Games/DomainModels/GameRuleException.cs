namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// Base for game-rule violations that the API surfaces to clients with a stable, machine-readable
/// <see cref="Code"/>. Derives from <see cref="InvalidOperationException"/> so existing catch clauses
/// and tests that expect an <see cref="InvalidOperationException"/> keep working.
/// </summary>
public abstract class GameRuleException : InvalidOperationException
{
    /// <summary>
    /// Stable, low-cardinality snake_case code the client maps to a localized message. Never include
    /// user- or game-specific values here — the code is a fixed enum-like token, not a description.
    /// </summary>
    public abstract string Code { get; }

    protected GameRuleException(string message) : base(message) { }
}

/// <summary>The caller is already a member of this game's lobby.</summary>
public sealed class PlayerAlreadyInLobbyException : GameRuleException
{
    public override string Code => "player_already_joined";

    public PlayerAlreadyInLobbyException() : base("This player is already in the lobby.") { }
}

/// <summary>The lobby has reached its maximum number of players.</summary>
public sealed class LobbyFullException : GameRuleException
{
    public override string Code => "lobby_full";

    public LobbyFullException(int maxLobbySize)
        : base($"The lobby is full: a game holds at most {maxLobbySize} players.") { }
}

/// <summary>The game is no longer accepting new players because it is not in the lobby state.</summary>
public sealed class GameNotJoinableException : GameRuleException
{
    public override string Code => "game_already_started";

    public GameNotJoinableException() : base("Players can only join a game that is in the lobby.") { }
}

/// <summary>The supplied join code does not match the game's code.</summary>
public sealed class InvalidJoinCodeException : GameRuleException
{
    public override string Code => "invalid_join_code";

    public InvalidJoinCodeException() : base("The join code is incorrect.") { }
}
