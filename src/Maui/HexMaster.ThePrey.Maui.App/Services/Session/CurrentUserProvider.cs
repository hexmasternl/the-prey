using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Session;

/// <summary>
/// Default <see cref="ICurrentUserProvider"/>. Resolves the caller's internal user id from
/// <c>GET /users/me</c> using <see cref="IAccessTokenProvider"/>, and caches it for the session. A
/// <c>401</c> invalidates the access token (consistent with the app's other authenticated calls).
/// </summary>
public sealed class CurrentUserProvider : ICurrentUserProvider
{
    private readonly IUserApiClient _userApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ILogger<CurrentUserProvider> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Guid? _cachedUserId;

    public CurrentUserProvider(
        IUserApiClient userApi,
        IAccessTokenProvider accessTokenProvider,
        ILogger<CurrentUserProvider> logger)
    {
        _userApi = userApi;
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;
    }

    public async Task<Guid?> GetUserIdAsync(CancellationToken ct = default)
    {
        if (_cachedUserId is { } cached && cached != Guid.Empty)
            return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cachedUserId is { } stillCached && stillCached != Guid.Empty)
                return stillCached;

            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is null)
                return null;

            var result = await _userApi.GetCurrentUserAsync(token, ct);
            switch (result.Outcome)
            {
                case UserSettingsOutcome.Success when result.Settings!.UserId != Guid.Empty:
                    _cachedUserId = result.Settings.UserId;
                    return _cachedUserId;

                case UserSettingsOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    return null;

                default:
                    _logger.LogWarning("Could not resolve the current user id ({Outcome}).", result.Outcome);
                    return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _cachedUserId = null;
}
