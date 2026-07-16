namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>A plain latitude/longitude reading, decoupled from the MAUI <c>Location</c> type.</summary>
public sealed record GpsFix(double Latitude, double Longitude);
