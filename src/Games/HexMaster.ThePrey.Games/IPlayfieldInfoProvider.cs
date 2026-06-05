using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games;

public sealed record PlayfieldInfo(string Name, IReadOnlyList<GpsCoordinateDto> Coordinates);

public interface IPlayfieldInfoProvider
{
    Task<PlayfieldInfo?> GetAsync(Guid playfieldId, CancellationToken ct);
}
