namespace HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;

public sealed record CreateUserRequest(
    string? FirstName,
    string? LastName,
    string EmailAddress,
    bool IsEmailVerified,
    string? PreferredLanguage);
