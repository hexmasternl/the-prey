namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of reading the current user's settings.</summary>
public enum UserSettingsOutcome
{
    Success,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IUserApiClient.GetCurrentUserAsync"/>.</summary>
public sealed record UserSettingsResult(UserSettingsOutcome Outcome, UserSettings? Settings)
{
    public static UserSettingsResult Success(UserSettings settings) => new(UserSettingsOutcome.Success, settings);
    public static readonly UserSettingsResult NotFound = new(UserSettingsOutcome.NotFound, null);
    public static readonly UserSettingsResult Unauthorized = new(UserSettingsOutcome.Unauthorized, null);
    public static readonly UserSettingsResult Error = new(UserSettingsOutcome.Error, null);
}

/// <summary>Outcome of saving the current user's settings.</summary>
public enum SaveSettingsOutcome
{
    Success,
    ValidationFailed,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IUserApiClient.UpdateUserAsync"/>.</summary>
public sealed record SaveSettingsResult(SaveSettingsOutcome Outcome, UserSettings? Settings)
{
    public static SaveSettingsResult Success(UserSettings settings) => new(SaveSettingsOutcome.Success, settings);
    public static readonly SaveSettingsResult ValidationFailed = new(SaveSettingsOutcome.ValidationFailed, null);
    public static readonly SaveSettingsResult NotFound = new(SaveSettingsOutcome.NotFound, null);
    public static readonly SaveSettingsResult Unauthorized = new(SaveSettingsOutcome.Unauthorized, null);
    public static readonly SaveSettingsResult Error = new(SaveSettingsOutcome.Error, null);
}

/// <summary>Calls the backend authorized users endpoints on behalf of the signed-in user.</summary>
public interface IUserApiClient
{
    /// <summary>Reads <c>GET /users/me</c> with the supplied bearer access token.</summary>
    Task<UserSettingsResult> GetCurrentUserAsync(string accessToken, CancellationToken ct = default);

    /// <summary>Updates the display name + preferred language via <c>PUT /users/me</c>.</summary>
    Task<SaveSettingsResult> UpdateUserAsync(UserSettings settings, string accessToken, CancellationToken ct = default);
}
