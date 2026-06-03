using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

public sealed class PlayfieldEditingContext
{
    public List<PlayfieldCoordinate> CurrentCoordinates { get; set; } = [];
    public PlayfieldCoordinate? CenterCoordinates { get; set; }
}
