using System.Runtime.CompilerServices;
using System.Threading.Channels;
using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Tests;

/// <summary>
/// Scriptable <see cref="IGameStreamClient"/> for the gameplay view-model tests. <see cref="Emit"/> queues
/// an event that the active subscription yields; the enumeration ends when its token is cancelled.
/// </summary>
internal sealed class FakeGameStreamClient : IGameStreamClient
{
    private readonly Channel<GameStreamEvent> _events = Channel.CreateUnbounded<GameStreamEvent>();

    public int SubscribeCount { get; private set; }
    public bool IsSubscribed { get; private set; }
    public bool Completed { get; private set; }

    public async IAsyncEnumerable<GameStreamEvent> Subscribe(
        Guid gameId, string accessToken, [EnumeratorCancellation] CancellationToken ct)
    {
        SubscribeCount++;
        IsSubscribed = true;
        try
        {
            while (true)
            {
                GameStreamEvent evt;
                try
                {
                    evt = await _events.Reader.ReadAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                yield return evt;
            }
        }
        finally
        {
            IsSubscribed = false;
            Completed = true;
        }
    }

    public void Emit(GameStreamEvent evt) => _events.Writer.TryWrite(evt);
}
