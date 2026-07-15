namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Reads the device's current position for the decorative HUD readout. Returns <c>null</c> when
/// permission is denied or no fix is available — it never throws or blocks the menu.
/// </summary>
public interface IGpsReader
{
    Task<GpsFix?> ReadAsync(CancellationToken ct = default);
}
