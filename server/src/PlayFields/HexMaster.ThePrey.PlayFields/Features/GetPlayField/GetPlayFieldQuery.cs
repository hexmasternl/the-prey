namespace HexMaster.ThePrey.PlayFields.Features.GetPlayField;

public sealed record GetPlayFieldQuery(Guid PlayFieldId, Guid RequestingOwnerId);
