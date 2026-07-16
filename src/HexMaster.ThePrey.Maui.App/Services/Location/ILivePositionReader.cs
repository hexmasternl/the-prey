namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Streams the device's own continuous local GPS fixes to the gameplay map's self marker while the page
/// is visible. Distinct from <see cref="IGpsReader"/> (a single one-shot read) and from position
/// <em>reporting</em> (the background-tracking capability): this reader renders locally only. Start on
/// appear, stop on disappear; a denied/absent fix simply produces no <see cref="PositionChanged"/> events
/// rather than throwing.
/// </summary>
public interface ILivePositionReader
{
    /// <summary>Raised on each new local fix while listening.</summary>
    event Action<GpsFix>? PositionChanged;

    /// <summary>Begins listening for foreground position updates (idempotent).</summary>
    void Start();

    /// <summary>Stops listening; safe to call when not started.</summary>
    void Stop();
}
