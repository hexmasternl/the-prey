namespace HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;

public sealed record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string DisplayName,
    string Language);
