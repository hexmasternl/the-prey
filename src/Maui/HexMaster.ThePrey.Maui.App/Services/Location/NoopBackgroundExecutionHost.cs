namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// A no-op <see cref="IBackgroundExecutionHost"/> for targets without a background-execution mechanism
/// (Windows, MacCatalyst). Tracking still runs while the app is in the foreground; there is no OS
/// keep-alive, matching the non-goal that desktop targets report only while active. Registered so DI
/// resolves the tracker on every platform.
/// </summary>
public sealed class NoopBackgroundExecutionHost : IBackgroundExecutionHost
{
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;
}
