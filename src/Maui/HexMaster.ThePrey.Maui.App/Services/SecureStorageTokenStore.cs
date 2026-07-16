using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services;

/// <summary>
/// <see cref="ITokenStore"/> backed by MAUI <see cref="SecureStorage"/> (OS keystore / keychain).
/// All operations degrade gracefully: a device without a secure store (or a locked keychain) is
/// treated as "no token" rather than crashing the app.
/// </summary>
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string RefreshTokenKey = "theprey.auth.refresh_token";

    private readonly ISecureStorage _secureStorage;
    private readonly ILogger<SecureStorageTokenStore> _logger;

    public SecureStorageTokenStore(ISecureStorage secureStorage, ILogger<SecureStorageTokenStore> logger)
    {
        _secureStorage = secureStorage;
        _logger = logger;
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            var value = await _secureStorage.GetAsync(RefreshTokenKey);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read refresh token from secure storage; treating as unauthenticated.");
            return null;
        }
    }

    public async Task SetRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        try
        {
            await _secureStorage.SetAsync(RefreshTokenKey, refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist refresh token to secure storage.");
        }
    }

    public void ClearRefreshToken()
    {
        try
        {
            _secureStorage.Remove(RefreshTokenKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear refresh token from secure storage.");
        }
    }
}
