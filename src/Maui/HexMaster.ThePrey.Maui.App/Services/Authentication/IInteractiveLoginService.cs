namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>Result of an interactive Auth0 sign-in attempt.</summary>
public enum InteractiveLoginOutcome
{
    /// <summary>Sign-in completed and a refresh token was stored.</summary>
    Success,

    /// <summary>The user dismissed or cancelled the system web authenticator.</summary>
    Cancelled,

    /// <summary>Sign-in failed (state mismatch, missing code, token exchange error, or transport error).</summary>
    Failed,

    /// <summary>Sign-in succeeded but Auth0 issued no refresh token (offline_access misconfiguration).</summary>
    NoRefreshToken
}

/// <summary>
/// Runs the full interactive Auth0 sign-in (PKCE authorize → system web authenticator →
/// code exchange → persist refresh token). Shared by the login page and the main menu so both
/// entry points drive the same tested flow. Callers map the returned outcome to their own UI.
/// </summary>
public interface IInteractiveLoginService
{
    Task<InteractiveLoginOutcome> LoginAsync(CancellationToken ct = default);
}
