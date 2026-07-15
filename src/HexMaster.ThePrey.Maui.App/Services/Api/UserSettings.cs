namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Client-side projection of the fields of the backend <c>UserDto</c> that the settings page
/// edits. Callsign/email are part of the backend model but are not used here.
/// </summary>
public sealed record UserSettings(string DisplayName, string PreferredLanguage);
