namespace HexMaster.ThePrey.Games.Features.TagPlayer;

public sealed record TagPlayerCommand(Guid GameId, Guid CallerId, Guid TargetParticipantId);

public sealed record TagPlayerResult;
