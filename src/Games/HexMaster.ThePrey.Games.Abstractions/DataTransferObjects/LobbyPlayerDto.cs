namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

public sealed record LobbyPlayerDto(Guid UserId, string DisplayName, string? ProfilePictureUrl, bool IsReady, bool DesignatedHunter);
