namespace HexMaster.ThePrey.Maui.App.Services.Platform;

/// <summary>
/// Abstracts the platform application-quit path (used for the Exit action). Programmatic quit is
/// well-behaved on Android/Windows/Mac but discouraged on iOS, so support is reported per platform.
/// </summary>
public interface IApplicationExit
{
    /// <summary>Whether the current platform permits a programmatic quit (false on iOS).</summary>
    bool IsExitSupported { get; }

    /// <summary>Quits the application where supported; a no-op otherwise.</summary>
    void Exit();
}
