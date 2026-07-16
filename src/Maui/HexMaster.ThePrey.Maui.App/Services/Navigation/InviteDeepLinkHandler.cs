using HexMaster.ThePrey.Maui.App.Configuration;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Default <see cref="IInviteDeepLinkHandler"/>. Accepts only an HTTPS link whose host and leading path
/// segment match the configured <see cref="ThePreyClientOptions.JoinLinkBaseUrl"/> (e.g.
/// <c>https://theprey.nl/join/{gameId}</c>) with a valid <see cref="Guid"/> last segment, and routes to the
/// <c>join</c> Shell route via the injected <see cref="IMenuNavigator"/>. Plain .NET (navigation behind the
/// menu-navigator seam) so it is fully unit-testable.
/// </summary>
public sealed class InviteDeepLinkHandler : IInviteDeepLinkHandler
{
    private readonly IMenuNavigator _navigator;
    private readonly ILogger<InviteDeepLinkHandler> _logger;
    private readonly string _expectedHost;
    private readonly string _expectedPathSegment;

    private Uri? _pending;

    public InviteDeepLinkHandler(
        IMenuNavigator navigator,
        IOptions<ThePreyClientOptions> options,
        ILogger<InviteDeepLinkHandler> logger)
    {
        _navigator = navigator;
        _logger = logger;

        // Derive the accepted host + leading path segment from the same option the lobby builds invite links
        // from, so the handler and the shared links never drift (e.g. host "theprey.nl", segment "join").
        var joinBase = new Uri(options.Value.JoinLinkBaseUrl, UriKind.Absolute);
        _expectedHost = joinBase.Host;
        _expectedPathSegment = joinBase.AbsolutePath.Trim('/');
    }

    public async Task<bool> TryHandleAsync(Uri? uri, CancellationToken ct = default)
    {
        if (!TryParseGameId(uri, out var gameId))
            return false;

        try
        {
            await _navigator.GoToAsync(
                $"{JoinGameViewModel.JoinRoute}?{GameLobbyViewModel.GameIdQueryKey}={gameId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to route the invite deep link.");
            return false;
        }
    }

    public void QueuePending(Uri? uri) => _pending = uri;

    public async Task ReplayPendingAsync(CancellationToken ct = default)
    {
        var pending = _pending;
        if (pending is null)
            return;

        _pending = null;
        await TryHandleAsync(pending, ct);
    }

    // Accepts only https://{expectedHost}/{expectedPathSegment}/{guid}. Anything else (wrong scheme/host/path,
    // extra segments, non-guid id) is rejected so other apps cannot route arbitrary links into the join page.
    private bool TryParseGameId(Uri? uri, out Guid gameId)
    {
        gameId = Guid.Empty;

        if (uri is null || !uri.IsAbsoluteUri)
            return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(uri.Host, _expectedHost, StringComparison.OrdinalIgnoreCase))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
            return false;
        if (!string.Equals(segments[0], _expectedPathSegment, StringComparison.OrdinalIgnoreCase))
            return false;

        return Guid.TryParse(segments[1], out gameId);
    }
}
