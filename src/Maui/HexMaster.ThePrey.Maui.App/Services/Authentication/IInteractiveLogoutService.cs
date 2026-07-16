namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>
/// Runs a full sign-out: clears the local refresh token AND ends the Auth0 tenant SSO session
/// (federated logout via the system web authenticator) so a subsequent login prompts for
/// credentials instead of silently resuming the previous account. The local token is always
/// cleared, even if the browser round-trip fails, so callers can unconditionally treat the app as
/// signed out afterwards.
/// </summary>
public interface IInteractiveLogoutService
{
    Task LogoutAsync(CancellationToken ct = default);
}
