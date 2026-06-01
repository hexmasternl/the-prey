using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Users.Features.UpdateUser;

public sealed class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand, UserDto>
{
    private readonly IUserRepository _users;
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    public UpdateUserCommandHandler(IUserRepository users, ILogger<UpdateUserCommandHandler> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<UserDto> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = UserActivitySource.Source.StartActivity("UpdateUser");
        activity?.SetTag("user.subject_id", command.SubjectId);

        try
        {
            var user = await _users.GetBySubjectIdAsync(command.SubjectId, ct)
                ?? throw new InvalidOperationException($"User not found for subject {command.SubjectId}.");

            activity?.SetTag("user.id", user.Id);

            user.Update(command.FirstName, command.LastName, command.DisplayName, command.Language);
            await _users.UpdateAsync(user, ct);

            _logger.LogInformation("User {UserId} updated display name and preferences", user.Id);

            return new UserDto(user.Id, user.DisplayName, user.EmailAddress, user.Language);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
