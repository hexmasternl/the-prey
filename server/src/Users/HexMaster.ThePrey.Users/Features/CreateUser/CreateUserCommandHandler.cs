using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.DomainModels;
using HexMaster.ThePrey.Users.Observability;
using HexMaster.ThePrey.Users.Services;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Users.Features.CreateUser;

public sealed class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    private readonly IUserRepository _users;
    private readonly IUserCacheService _cache;
    private readonly IUserMetrics _metrics;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IUserRepository users,
        IUserCacheService cache,
        IUserMetrics metrics,
        ILogger<CreateUserCommandHandler> logger)
    {
        _users = users;
        _cache = cache;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<CreateUserResult> Handle(CreateUserCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = UserActivitySource.Source.StartActivity("CreateUser");
        activity?.SetTag("user.subject_id", command.SubjectId);

        try
        {
            var existing = await _users.GetBySubjectIdAsync(command.SubjectId, ct);

            if (existing is not null)
            {
                existing.SyncFromAuth(command.FirstName, command.LastName, command.EmailAddress, command.IsEmailVerified);
                await _users.UpdateAsync(existing, ct);

                await _cache.SetAsync(
                    command.SubjectId,
                    new UserCacheEntry(existing.Id, existing.Callsign, existing.PreferredLanguage),
                    ct);

                _logger.LogInformation("User {SubjectId} synced from Auth0 login", command.SubjectId);

                return new CreateUserResult(false, ToDto(existing));
            }

            var user = User.Create(
                command.SubjectId,
                command.FirstName,
                command.LastName,
                command.EmailAddress,
                command.IsEmailVerified,
                command.PreferredLanguage);

            await _users.AddAsync(user, ct);

            await _cache.SetAsync(
                command.SubjectId,
                new UserCacheEntry(user.Id, user.Callsign, user.PreferredLanguage),
                ct);

            _metrics.RecordUserCreated();
            _logger.LogInformation("User {UserId} created for subject {SubjectId}", user.Id, command.SubjectId);

            activity?.SetTag("user.id", user.Id);

            return new CreateUserResult(true, ToDto(user));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private static UserDto ToDto(DomainModels.User user) =>
        new(user.Id, user.DisplayName, user.Callsign, user.EmailAddress, user.PreferredLanguage);
}
