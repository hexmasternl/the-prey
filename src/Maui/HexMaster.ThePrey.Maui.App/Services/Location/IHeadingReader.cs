namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Streams the device compass heading (degrees clockwise from north) to rotate the gameplay map's self
/// arrow while the page is visible. Start on appear, stop on disappear; an unavailable compass simply
/// produces no <see cref="HeadingChanged"/> events rather than throwing (the arrow then renders without
/// rotation).
/// </summary>
public interface IHeadingReader
{
    /// <summary>Raised on each new compass reading while listening, in degrees clockwise from north.</summary>
    event Action<double>? HeadingChanged;

    /// <summary>Begins listening for compass updates (idempotent).</summary>
    void Start();

    /// <summary>Stops listening; safe to call when not started.</summary>
    void Stop();
}
