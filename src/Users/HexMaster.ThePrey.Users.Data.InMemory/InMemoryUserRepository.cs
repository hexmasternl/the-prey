using HexMaster.ThePrey.Users.DomainModels;
using System.Collections.Concurrent;

namespace HexMaster.ThePrey.Users.Data.InMemory;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _store = new(StringComparer.Ordinal);

    public Task<User?> GetBySubjectIdAsync(string subjectId, CancellationToken ct)
    {
        _store.TryGetValue(subjectId, out var user);
        return Task.FromResult(user);
    }

    public Task AddAsync(User user, CancellationToken ct)
    {
        if (!_store.TryAdd(user.SubjectId, user))
            throw new InvalidOperationException($"A user with subject ID '{user.SubjectId}' already exists.");

        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct)
    {
        _store[user.SubjectId] = user;
        return Task.CompletedTask;
    }
}
