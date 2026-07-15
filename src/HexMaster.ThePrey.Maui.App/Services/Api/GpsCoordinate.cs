namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// A single latitude/longitude vertex of a playfield polygon, the client-side counterpart of the
/// backend <c>GpsCoordinateDto</c>. Kept in the API namespace because it is part of the create seam;
/// serialized to the backend's <c>{ latitude, longitude }</c> JSON shape.
/// </summary>
public sealed record GpsCoordinate(double Latitude, double Longitude);
