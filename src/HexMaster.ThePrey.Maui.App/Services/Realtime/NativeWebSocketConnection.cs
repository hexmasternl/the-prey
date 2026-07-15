using System.Net.WebSockets;
using System.Text;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// <see cref="IWebSocketConnection"/> over a native <see cref="ClientWebSocket"/>. Assembles multi-frame
/// text messages and maps any close/transport failure on receive to a <c>null</c> result so the caller
/// treats it uniformly as "socket closed → reconnect". This class touches only plain-.NET APIs but is
/// excluded from the unit-test build (tests drive a fake); it exists so production wires a real socket.
/// </summary>
public sealed class NativeWebSocketConnection : IWebSocketConnection
{
    private readonly ClientWebSocket _socket = new();
    private readonly byte[] _receiveBuffer = new byte[8192];

    public async Task ConnectAsync(Uri uri, string subProtocol, CancellationToken ct)
    {
        _socket.Options.AddSubProtocol(subProtocol);
        await _socket.ConnectAsync(uri, ct);
    }

    public Task SendTextAsync(string message, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    public async Task<string?> ReceiveTextAsync(CancellationToken ct)
    {
        using var message = new MemoryStream();
        while (true)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(_receiveBuffer), ct);
            }
            catch (WebSocketException)
            {
                return null; // Transport dropped — signal reconnect.
            }

            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            message.Write(_receiveBuffer, 0, result.Count);
            if (result.EndOfMessage)
                break;
        }

        return Encoding.UTF8.GetString(message.ToArray());
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, ct);
        }
        catch
        {
            // Best-effort close; a failing close is never a caller concern.
        }
    }

    public ValueTask DisposeAsync()
    {
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Production factory: hands out a fresh <see cref="NativeWebSocketConnection"/> per attempt.</summary>
public sealed class NativeWebSocketConnectionFactory : IWebSocketConnectionFactory
{
    public IWebSocketConnection Create() => new NativeWebSocketConnection();
}
