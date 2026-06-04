using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.Observability;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace HexMaster.ThePrey.Users.Features.ResolveUserBySubject;

public sealed class ResolveUserBySubjectQueryHandler : IQueryHandler<ResolveUserBySubjectQuery, UserDto?>
{
    private readonly IUserRepository _users;
    private readonly ILogger<ResolveUserBySubjectQueryHandler> _logger;

    public ResolveUserBySubjectQueryHandler(
        IUserRepository users,
        ILogger<ResolveUserBySubjectQueryHandler> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<UserDto?> Handle(ResolveUserBySubjectQuery query, CancellationToken ct)
    {
        using var activity = UserActivitySource.Source.StartActivity("ResolveUserBySubject");

        try
        {
            var user = await _users.GetBySubjectIdAsync(query.SubjectId, ct);

            activity?.SetTag("user.found", user is not null);

            if (user is null)
            {
                _logger.LogInformation("No user found for subject {SubjectId}", query.SubjectId);
                return null;
            }

            return new UserDto(user.Id, user.DisplayName, user.Callsign, user.EmailAddress, user.PreferredLanguage);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
