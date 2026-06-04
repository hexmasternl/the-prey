namespace HexMaster.ThePrey.Users.Features.UpdateUser;

public sealed record UpdateUserCommand(
    string SubjectId,
    string? FirstName,
    string? LastName,
    string DisplayName,
    string PreferredLanguage);
