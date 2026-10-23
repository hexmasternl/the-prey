namespace HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;

/// <summary>Update the caller's game settings: in-game callsign and app language ("en" or "nl").</summary>
public sealed record UpdateUserSettingsRequest(string Callsign, string PreferredLanguage);
