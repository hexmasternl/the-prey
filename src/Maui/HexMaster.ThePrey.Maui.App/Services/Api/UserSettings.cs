namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Client-side projection of the fields of the backend <c>UserDto</c> that the settings page
/// edits. Callsign/email are part of the backend model but are not used here. <see cref="UserId"/>
/// is the caller's internal user id (bound from <c>GET /users/me</c>) — used to determine role
/// (hunter vs prey) at the gameplay hand-off; it is left at its default when constructing an update body.
/// </summary>
public sealed record UserSettings(string DisplayName, string PreferredLanguage, Guid UserId = default);
