using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Users.Features.UpdateUser;

public sealed record UpdateUserCommand(
    string SubjectId,
    string? FirstName,
    string? LastName,
    string DisplayName,
    string Language);
