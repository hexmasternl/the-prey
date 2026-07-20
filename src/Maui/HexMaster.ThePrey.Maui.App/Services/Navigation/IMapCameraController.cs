namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// The HUD's follow / free-pan signal to the hosting map. The HUD owns the Center toggle state and
/// emits the intent through this seam; the (separately-owned) gameplay map reads <see cref="IsFollowing"/>
/// and listens to <see cref="FollowModeChanged"/> to decide how to keep the player centred. Kept behind
/// an interface so the HUD view model stays free of MAUI map types and unit-testable.
/// </summary>
public interface IMapCameraController
{
    /// <summary>Whether the map should currently keep the camera pinned to the device location.</summary>
    bool IsFollowing { get; }

    /// <summary>
    /// Raised whenever the follow mode is (re)asserted, carrying the new value. Raised on every
    /// <see cref="SetFollowMode"/> call rather than only on a change, so the HUD re-asserting the current
    /// mode on activation also recentres a map host that attached after the last toggle.
    /// </summary>
    event EventHandler<bool>? FollowModeChanged;

    /// <summary>
    /// Signals whether the map should keep the player fixed at the device's current location
    /// (<paramref name="follow"/> is <c>true</c>) or allow free panning (<c>false</c>).
    /// </summary>
    void SetFollowMode(bool follow);
}
