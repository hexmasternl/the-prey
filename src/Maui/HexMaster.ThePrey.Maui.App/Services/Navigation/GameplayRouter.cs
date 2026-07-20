using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// The gameplay router. As the app's <see cref="ILobbyNavigator"/>, its <see cref="GoToGameplayAsync"/>
/// resolves the caller's active game, reads its <c>HunterUserId</c>, compares it to the caller's internal
/// id (<see cref="ICurrentUserProvider"/>), and routes to the hunter or prey game page accordingly — the
/// role branch lives here, at the entry, so each page stays single-role. It also implements
/// <see cref="IGameplayNavigator.GoToOutcomeAsync"/>. All Shell navigation goes through the testable
/// <see cref="IMenuNavigator"/> seam; role resolution defaults to the prey page when it cannot be
/// determined (the page then self-resolves and surfaces its own state).
/// </summary>
public sealed class GameplayRouter : ILobbyNavigator, IGameplayNavigator
{
    /// <summary>Shell route for the hunter game play page.</summary>
    public const string HunterGameRoute = "gameplay-hunter";

    /// <summary>Shell route for the prey game play page.</summary>
    public const string PreyGameRoute = "gameplay-prey";

    private readonly IGameApiClient _gameApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IMenuNavigator _navigator;
    private readonly IOutcomeNavigator _outcomeNavigator;
    private readonly ILogger<GameplayRouter> _logger;

    public GameplayRouter(
        IGameApiClient gameApi,
        IAccessTokenProvider accessTokenProvider,
        ICurrentUserProvider currentUser,
        IMenuNavigator navigator,
        IOutcomeNavigator outcomeNavigator,
        ILogger<GameplayRouter> logger)
    {
        _gameApi = gameApi;
        _accessTokenProvider = accessTokenProvider;
        _currentUser = currentUser;
        _navigator = navigator;
        _outcomeNavigator = outcomeNavigator;
        _logger = logger;
    }

    public Task GoToGameplayAsync() => RouteToRoleAsync(replaceCurrentPage: true, CancellationToken.None);

    public Task ResumeGameplayAsync() => RouteToRoleAsync(replaceCurrentPage: false, CancellationToken.None);

    /// <summary>
    /// Fulfils the gameplay pages' game-ended hand-off by delegating to the outcome navigator, which owns
    /// the outcome route, its query parameters, and the once-per-game guard.
    /// </summary>
    public Task GoToOutcomeAsync(Guid gameId, bool isHunter) =>
        _outcomeNavigator.GoToOutcomeAsync(gameId, isHunter);

    /// <summary>
    /// Resolves the role route and navigates to it. <paramref name="replaceCurrentPage"/> selects the
    /// hand-off's stack semantics: the lobby is replaced (it must not survive underneath), while the
    /// main menu is pushed over (it is the Shell root, so <c>ReplaceAsync</c>'s leading <c>..</c> has
    /// nothing to pop, and the player should be able to back out to the menu).
    /// </summary>
    private async Task RouteToRoleAsync(bool replaceCurrentPage, CancellationToken ct)
    {
        var route = await ResolveRoleRouteAsync(ct);
        if (replaceCurrentPage)
        {
            await _navigator.ReplaceAsync(route);
        }
        else
        {
            await _navigator.GoToAsync(route);
        }
    }

    /// <summary>Resolves the hunter route when the caller is this game's hunter; otherwise the prey route.</summary>
    private async Task<string> ResolveRoleRouteAsync(CancellationToken ct)
    {
        var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
        if (token is null)
        {
            _logger.LogWarning("Gameplay hand-off could not acquire a token; defaulting to the prey page.");
            return PreyGameRoute;
        }

        var active = await _gameApi.GetActiveGameAsync(token, ct);
        if (active.Outcome != ActiveGameOutcome.HasActiveGame || active.Game is null)
        {
            _logger.LogWarning("Gameplay hand-off found no active game ({Outcome}); defaulting to the prey page.", active.Outcome);
            return PreyGameRoute;
        }

        var game = await _gameApi.GetGameAsync(active.Game.GameId, token, ct);
        if (game.Outcome != GetGameOutcome.Success || game.Game?.HunterUserId is not { } hunterUserId)
        {
            _logger.LogWarning("Gameplay hand-off could not read the game's hunter ({Outcome}); defaulting to the prey page.", game.Outcome);
            return PreyGameRoute;
        }

        var currentUserId = await _currentUser.GetUserIdAsync(ct);
        return currentUserId is { } me && me == hunterUserId ? HunterGameRoute : PreyGameRoute;
    }
}
