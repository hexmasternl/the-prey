using HexMaster.ThePrey.Maui.App.Services.Platform;

namespace HexMaster.ThePrey.Maui.App.Tests;

/// <summary>
/// Test <see cref="IUiDispatcher"/> that runs the action inline, so a view model's real-time callback is
/// exercised synchronously (the production dispatcher does the same when already on the UI thread).
/// </summary>
internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Dispatch(Action action) => action();
}

/// <summary>
/// Test <see cref="IUiDispatcher"/> that queues instead of running, modelling a caller that is NOT on the
/// UI thread. <see cref="RunPending"/> pumps the queue, so a test can prove a callback was marshalled
/// rather than executed inline on the publishing thread.
/// </summary>
internal sealed class QueueingUiDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _pending = new();

    public void Dispatch(Action action) => _pending.Enqueue(action);

    /// <summary>Runs everything queued so far (actions queued while running are picked up too).</summary>
    public void RunPending()
    {
        while (_pending.Count > 0)
            _pending.Dequeue()();
    }
}
