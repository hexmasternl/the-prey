using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Placeholder <see cref="IMapCameraController"/> registered so the HUD view model can be constructed
/// while the gameplay map is still owned by a separate change. It only records the follow/free-pan
/// signal; the real map registers its own implementation (replacing this one in DI) to actually move
/// the camera.
/// </summary>
public sealed class NullMapCameraController : IMapCameraController
{
    private readonly ILogger<NullMapCameraController> _logger;

    public NullMapCameraController(ILogger<NullMapCameraController> logger) => _logger = logger;

    public void SetFollowMode(bool follow) =>
        _logger.LogDebug("Map follow-mode signal received ({Follow}); no map host is wired yet.", follow);
}
