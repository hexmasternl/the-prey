namespace HexMaster.ThePrey.Games.Features.CheckAppVersion;

/// <summary>Outcome of comparing a client version against the configured minimum.</summary>
public enum AppVersionCheckResult
{
    /// <summary>The client version meets or exceeds the minimum (or no minimum is configured).</summary>
    UpToDate,

    /// <summary>The client version is below the configured minimum; the client must update.</summary>
    UpdateRequired
}
