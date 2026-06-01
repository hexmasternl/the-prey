using HexMaster.ThePrey.Users.DomainModels;

namespace HexMaster.ThePrey.Users;

public interface IUserRepository
{
    Task<User?> GetBySubjectIdAsync(string subjectId, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
}
