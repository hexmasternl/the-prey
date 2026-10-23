using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.PlayFields.DomainModels;

namespace HexMaster.ThePrey.PlayFields;

internal static class PlayFieldMappings
{
    internal static PlayFieldDto ToDto(this PlayField playField) =>
        new(
            playField.Id,
            playField.Name,
            playField.OwnerId,
            playField.IsPublic,
            playField.Points.Select(p => new GpsCoordinateDto(p.Latitude, p.Longitude)).ToList(),
            playField.LastModifiedOn,
            playField.CenterCoordinates is { } c ? new GpsCoordinateDto(c.Latitude, c.Longitude) : null);

    internal static PlayFieldSummaryDto ToSummaryDto(this PlayField playField) =>
        new(
            playField.Id,
            playField.Name,
            playField.OwnerId,
            playField.IsPublic,
            playField.LastModifiedOn,
            playField.CenterCoordinates is { } c ? new GpsCoordinateDto(c.Latitude, c.Longitude) : null);
}
