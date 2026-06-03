using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.JoinGame;

public sealed record JoinGameCommand(
    Guid GameId,
    Guid UserId,
    string DisplayName,
    string? ProfilePictureUrl);

public sealed record JoinGameResult(GameDto Game);
