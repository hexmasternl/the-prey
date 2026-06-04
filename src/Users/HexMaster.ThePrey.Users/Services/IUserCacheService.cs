namespace HexMaster.ThePrey.Users.Services;

public interface IUserCacheService
{
    Task<UserCacheEntry?> GetAsync(string subjectId, CancellationToken ct);
    Task SetAsync(string subjectId, UserCacheEntry entry, CancellationToken ct);
}
