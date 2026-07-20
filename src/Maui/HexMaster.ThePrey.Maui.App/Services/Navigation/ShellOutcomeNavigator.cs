using System.Globalization;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Default <see cref="IOutcomeNavigator"/> backed by the app <see cref="Shell"/>. A singleton, so the
/// once-per-game guard survives the gameplay page being torn down mid-hand-off.
/// </summary>
public sealed class ShellOutcomeNavigator : IOutcomeNavigator
{
    /// <summary>Shell route for the post-game outcome page.</summary>
    public const string OutcomeRoute = "game-outcome";

    /// <summary>Query keys the outcome page reads back off the route.</summary>
    public const string GameIdQueryKey = "gameId";
    public const string IsHunterQueryKey = "isHunter";

    private readonly object _gate = new();
    private Guid _handedOffGameId;

    public Task GoToOutcomeAsync(Guid gameId, bool isHunter)
    {
        lock (_gate)
        {
            // The end can be observed twice (a late status poll plus the real-time game-ended event).
            // Only the first observation navigates; the rest are dropped.
            if (_handedOffGameId == gameId && gameId != Guid.Empty)
                return Task.CompletedTask;
            _handedOffGameId = gameId;
        }

        var route = string.Create(
            CultureInfo.InvariantCulture,
            $"{OutcomeRoute}?{GameIdQueryKey}={gameId}&{IsHunterQueryKey}={isHunter}");
        return Shell.Current.GoToAsync(route);
    }

    /// <summary>
    /// Resets to the Shell's root content (<c>welcome</c>), which drops every pushed page — outcome,
    /// gameplay and lobby alike — from the navigation stack. The welcome bootstrap then routes straight
    /// on to the <c>home</c> main menu, exactly as it does at startup and after sign-in. An absolute
    /// <c>//home</c> is not an option: <c>home</c> is a <see cref="Routing.RegisterRoute"/> target, not a
    /// <c>ShellContent</c>.
    /// </summary>
    public Task ReturnToMenuAsync()
    {
        lock (_gate)
        {
            // The game is done with; allow a future game to hand off again.
            _handedOffGameId = Guid.Empty;
        }

        return Shell.Current.GoToAsync("//welcome");
    }
}
