using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Users.Features.UpdateUserSettings;

public sealed class UpdateUserSettingsCommandHandler : ICommandHandler<UpdateUserSettingsCommand, UserDto>
{
    private readonly IUserRepository _users;
    private readonly ILogger<UpdateUserSettingsCommandHandler> _logger;

    public UpdateUserSettingsCommandHandler(IUserRepository users, ILogger<UpdateUserSettingsCommandHandler> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<UserDto> Handle(UpdateUserSettingsCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = UserActivitySource.Source.StartActivity("UpdateUserSettings");
        activity?.SetTag("user.subject_id", command.SubjectId);

        try
        {
            var user = await _users.GetBySubjectIdAsync(command.SubjectId, ct)
                ?? throw new InvalidOperationException($"User not found for subject {command.SubjectId}.");

            activity?.SetTag("user.id", user.Id);
            activity?.SetTag("user.preferred_language", command.PreferredLanguage);

            user.UpdateSettings(command.Callsign, command.PreferredLanguage);
            await _users.UpdateAsync(user, ct);

            _logger.LogInformation("User {UserId} updated game settings", user.Id);

            return new UserDto(user.Id, user.DisplayName, user.Callsign, user.EmailAddress, user.PreferredLanguage);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
