namespace HexMaster.ThePrey.Games.Features.ExportGames;

public sealed record ExportGamesQuery(DateTimeOffset FromInclusive, DateTimeOffset ToExclusive);
