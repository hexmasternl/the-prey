namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// The shared follow/free-pan state between the HUD's Center toggle and whichever gameplay map is on
/// screen. A singleton rather than map-owned state: the HUD view model and the map page are separate
/// objects with separate lifetimes, and the HUD asserts the mode on activation — possibly before the map
/// has subscribed. Holding the value here means a late subscriber can read <see cref="IsFollowing"/> and
/// centre itself immediately instead of waiting for the next toggle.
/// </summary>
public sealed class MapFollowModeController : IMapCameraController
{
    // Follow is the default: the HUD's toggle starts on, so a fresh game opens pinned to the player.
    public bool IsFollowing { get; private set; } = true;

    public event EventHandler<bool>? FollowModeChanged;

    public void SetFollowMode(bool follow)
    {
        IsFollowing = follow;
        FollowModeChanged?.Invoke(this, follow);
    }
}
