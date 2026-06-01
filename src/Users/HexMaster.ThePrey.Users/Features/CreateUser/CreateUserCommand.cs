using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Users.Features.CreateUser;

public sealed record CreateUserCommand(
    string SubjectId,
    string? FirstName,
    string? LastName,
    string EmailAddress,
    bool IsEmailVerified,
    string Language);

public sealed record CreateUserResult(bool WasCreated, UserDto User);
