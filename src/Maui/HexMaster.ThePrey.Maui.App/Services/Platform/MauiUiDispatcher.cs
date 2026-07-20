namespace HexMaster.ThePrey.Maui.App.Services.Platform;

/// <summary>
/// MAUI-backed <see cref="IUiDispatcher"/>. Runs inline when the caller is already on the UI thread so a
/// synchronous path (a button command) keeps its ordering, and posts otherwise.
/// </summary>
public sealed class MauiUiDispatcher : IUiDispatcher
{
    public void Dispatch(Action action)
    {
        if (MainThread.IsMainThread)
            action();
        else
            MainThread.BeginInvokeOnMainThread(action);
    }
}
