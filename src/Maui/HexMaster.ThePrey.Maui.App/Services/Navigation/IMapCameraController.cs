namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// The HUD's follow / free-pan signal to the hosting map. The HUD owns the Center toggle state and
/// emits the intent through this seam; the (separately-owned) gameplay map implements it and decides
/// how to keep the player centred. Kept behind an interface so the HUD view model stays free of MAUI
/// map types and unit-testable.
/// </summary>
public interface IMapCameraController
{
    /// <summary>
    /// Signals whether the map should keep the player fixed at the device's current location
    /// (<paramref name="follow"/> is <c>true</c>) or allow free panning (<c>false</c>).
    /// </summary>
    void SetFollowMode(bool follow);
}
