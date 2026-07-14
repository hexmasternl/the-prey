namespace HexMaster.ThePrey.Maui.App.Services.Platform;

/// <summary>Default <see cref="IApplicationExit"/>. Quits via the MAUI application on platforms
/// that allow it; on iOS a programmatic quit is discouraged, so Exit is unsupported.</summary>
public sealed class ApplicationExit : IApplicationExit
{
#if IOS
    public bool IsExitSupported => false;

    public void Exit()
    {
        // iOS Human Interface Guidelines discourage programmatic termination — no-op.
    }
#else
    public bool IsExitSupported => true;

    public void Exit() => Application.Current?.Quit();
#endif
}
