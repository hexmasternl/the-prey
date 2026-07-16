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

    /// <summary>Shell route for the post-game outcome page (placeholder until the outcome change lands).</summary>
    public const string OutcomeRoute = "game-outcome";

    private readonly IGameApiClient _gameApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IMenuNavigator _navigator;
    private readonly ILogger<GameplayRouter> _logger;

    public GameplayRouter(
        IGameApiClient gameApi,
        IAccessTokenProvider accessTokenProvider,
        ICurrentUserProvider currentUser,
        IMenuNavigator navigator,
        ILogger<GameplayRouter> logger)
    {
        _gameApi = gameApi;
        _accessTokenProvider = accessTokenProvider;
        _currentUser = currentUser;
        _navigator = navigator;
        _logger = logger;
    }

    public Task GoToGameplayAsync() => RouteToRoleAsync(CancellationToken.None);

    public Task GoToOutcomeAsync() => _navigator.GoToAsync(OutcomeRoute);

    private async Task RouteToRoleAsync(CancellationToken ct)
    {
        var route = await ResolveRoleRouteAsync(ct);
        await _navigator.GoToAsync(route);
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
