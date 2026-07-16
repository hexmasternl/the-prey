namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// A single position fix for background reporting: coordinates plus the accuracy (metres, when the
/// platform provides it) and the capture timestamp. Richer than <see cref="GpsFix"/> (which the
/// decorative HUD uses) because the backend report carries accuracy and the recorded time.
/// </summary>
public sealed record LocationSample(
    double Latitude,
    double Longitude,
    double? Accuracy,
    DateTimeOffset RecordedAt);
