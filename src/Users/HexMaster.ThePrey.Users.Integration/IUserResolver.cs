using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Users.Integration;

public interface IUserResolver
{
    Task<UserDto?> ResolveUser(string subjectId, CancellationToken ct = default);
}
