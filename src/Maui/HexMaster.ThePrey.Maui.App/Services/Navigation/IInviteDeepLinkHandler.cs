namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Parses an incoming invite <see cref="Uri"/> (<c>https://theprey.nl/join/{gameId}</c>) and routes to the
/// Join Game page with the game id. Keeps URI parsing/validation and routing in one testable place so the
/// platform glue (running-app and cold-start launch) stays a thin pass-through.
/// </summary>
public interface IInviteDeepLinkHandler
{
    /// <summary>
    /// Validates <paramref name="uri"/> and, when it is a well-formed join link, navigates to the <c>join</c>
    /// Shell route with the parsed game id. Malformed / wrong-scheme / wrong-host / wrong-path / non-guid
    /// URIs are ignored. Returns <c>true</c> only when it routed. Never throws.
    /// </summary>
    Task<bool> TryHandleAsync(Uri? uri, CancellationToken ct = default);

    /// <summary>
    /// Stores a link captured from a cold-start launch (before the Shell is ready) to be replayed once
    /// navigation is possible via <see cref="ReplayPendingAsync"/>. A later queued link supersedes an
    /// earlier un-replayed one.
    /// </summary>
    void QueuePending(Uri? uri);

    /// <summary>
    /// Replays the link queued by <see cref="QueuePending"/> (if any) through <see cref="TryHandleAsync"/>.
    /// Returns <c>true</c> only when a queued link was present and it routed — so the caller can decide the
    /// post-boot destination (honor the invite) versus its default (the main menu).
    /// </summary>
    Task<bool> ReplayPendingAsync(CancellationToken ct = default);
}
