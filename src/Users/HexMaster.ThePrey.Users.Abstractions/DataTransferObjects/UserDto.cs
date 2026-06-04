namespace HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;

public sealed record UserDto(
    Guid UserId,
    string DisplayName,
    string Callsign,
    string EmailAddress,
    string PreferredLanguage);
