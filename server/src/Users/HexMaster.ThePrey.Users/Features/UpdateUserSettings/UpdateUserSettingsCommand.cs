namespace HexMaster.ThePrey.Users.Features.UpdateUserSettings;

public sealed record UpdateUserSettingsCommand(
    string SubjectId,
    string Callsign,
    string PreferredLanguage);
