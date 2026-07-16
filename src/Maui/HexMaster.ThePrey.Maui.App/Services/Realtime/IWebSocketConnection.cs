namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Minimal seam over a single client WebSocket, so the real-time connection logic can be unit-tested
/// against a fake instead of a live <see cref="System.Net.WebSockets.ClientWebSocket"/>. A connection
/// is single-use: open once, exchange text frames, close, dispose.
/// </summary>
public interface IWebSocketConnection : IAsyncDisposable
{
    /// <summary>Opens the socket to <paramref name="uri"/>, negotiating <paramref name="subProtocol"/>.</summary>
    Task ConnectAsync(Uri uri, string subProtocol, CancellationToken ct);

    /// <summary>Sends one complete UTF-8 text message.</summary>
    Task SendTextAsync(string message, CancellationToken ct);

    /// <summary>
    /// Receives the next complete text message, or <c>null</c> once the socket has closed (a close frame,
    /// a transport error, or the far end going away). A <c>null</c> return means "reconnect".
    /// </summary>
    Task<string?> ReceiveTextAsync(CancellationToken ct);

    /// <summary>Closes the socket gracefully if it is still open; never throws.</summary>
    Task CloseAsync(CancellationToken ct);
}

/// <summary>Creates a fresh <see cref="IWebSocketConnection"/> for each (re)connect attempt.</summary>
public interface IWebSocketConnectionFactory
{
    IWebSocketConnection Create();
}
