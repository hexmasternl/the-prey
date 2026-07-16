using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Session;

/// <summary>The three destinations the startup bootstrap can resolve to.</summary>
public enum SessionOutcome
{
    ActiveGame,
    NoActiveGame,
    Unauthenticated
}

/// <summary>Result of establishing a session on startup.</summary>
public sealed record SessionResult(SessionOutcome Outcome, GameStatus? Game)
{
    public static SessionResult Active(GameStatus game) => new(SessionOutcome.ActiveGame, game);
    public static readonly SessionResult NoGame = new(SessionOutcome.NoActiveGame, null);
    public static readonly SessionResult Unauthenticated = new(SessionOutcome.Unauthenticated, null);
}

/// <summary>
/// Orchestrates the startup flow: stored refresh token → access token → active-game check,
/// mapping the combined result to a single <see cref="SessionResult"/>.
/// </summary>
public interface ISessionService
{
    Task<SessionResult> TryEstablishSessionAsync(CancellationToken ct = default);
}
