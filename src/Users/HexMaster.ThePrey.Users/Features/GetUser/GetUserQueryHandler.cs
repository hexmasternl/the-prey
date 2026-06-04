using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.Observability;
using HexMaster.ThePrey.Users.Services;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Users.Features.GetUser;

public sealed class GetUserQueryHandler : IQueryHandler<GetUserQuery, UserDto?>
{
    private readonly IUserRepository _users;
    private readonly IUserCacheService _cache;
    private readonly ILogger<GetUserQueryHandler> _logger;

    public GetUserQueryHandler(
        IUserRepository users,
        IUserCacheService cache,
        ILogger<GetUserQueryHandler> logger)
    {
        _users = users;
        _cache = cache;
        _logger = logger;
    }

    public async Task<UserDto?> Handle(GetUserQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = UserActivitySource.Source.StartActivity("GetUser");
        activity?.SetTag("user.subject_id", query.SubjectId);

        var user = await _users.GetBySubjectIdAsync(query.SubjectId, ct);

        if (user is null)
        {
            _logger.LogWarning("User not found for subject {SubjectId}", query.SubjectId);
            return null;
        }

        activity?.SetTag("user.id", user.Id);

        await _cache.SetAsync(
            query.SubjectId,
            new UserCacheEntry(user.Id, user.Callsign, user.PreferredLanguage),
            ct);

        return new UserDto(user.Id, user.DisplayName, user.Callsign, user.EmailAddress, user.PreferredLanguage);
    }
}
