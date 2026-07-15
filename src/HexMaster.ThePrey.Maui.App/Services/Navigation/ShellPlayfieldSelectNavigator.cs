using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Shell-backed <see cref="IPlayfieldSelectNavigator"/> + <see cref="IPlayfieldSelectResultSink"/>. A
/// single singleton carries the in-flight selection: <see cref="SelectPlayfieldAsync"/> creates a
/// <see cref="TaskCompletionSource{TResult}"/>, presents <c>SelectPlayfieldPage</c> modally, and returns
/// its task; the modal's view model calls <see cref="CompleteAsync"/> on confirm (with the summary) or
/// cancel/system-back (with <c>null</c>). Completion is guarded so confirm and cancel can never
/// double-complete the source or double-pop the modal. Only one modal is open at a time.
/// </summary>
public sealed class ShellPlayfieldSelectNavigator : IPlayfieldSelectNavigator, IPlayfieldSelectResultSink
{
    /// <summary>Shell route for the playfield-selection modal.</summary>
    public const string SelectPlayfieldRoute = "select-playfield";

    private TaskCompletionSource<PlayFieldSummary?>? _completion;

    public async Task<PlayFieldSummary?> SelectPlayfieldAsync(CancellationToken ct = default)
    {
        // RunContinuationsAsynchronously so the caller's await resumes off the completion call stack.
        var completion = new TaskCompletionSource<PlayFieldSummary?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;

        await using (ct.Register(() => TryComplete(null)))
        {
            await Shell.Current.GoToAsync(SelectPlayfieldRoute);
            return await completion.Task;
        }
    }

    public async Task CompleteAsync(PlayFieldSummary? result)
    {
        // Only the first completion pops the modal; a later confirm/cancel race is a no-op.
        if (TryComplete(result))
            await Shell.Current.GoToAsync("..");
    }

    // Completes the pending source exactly once; returns true only for the winning call.
    private bool TryComplete(PlayFieldSummary? result)
    {
        var completion = _completion;
        if (completion is null)
            return false;

        _completion = null;
        return completion.TrySetResult(result);
    }
}
