using Dapr.Client;
using Microsoft.Extensions.Logging;
using ThePrey.Aspire.ServiceDefaults;

namespace HexMaster.ThePrey.Users.Services;

public sealed class UserCacheService : IUserCacheService
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<UserCacheService> _logger;

    public UserCacheService(DaprClient daprClient, ILogger<UserCacheService> logger)
    {
        _daprClient = daprClient;
        _logger = logger;
    }

    public async Task<UserCacheEntry?> GetAsync(string subjectId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var key = CacheKey(subjectId);

        try
        {
            return await _daprClient.GetStateAsync<UserCacheEntry>(AspireConstants.Resources.DaprStateStore, key, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for key {CacheKey}; treating as miss", key);
            return null;
        }
    }

    public async Task SetAsync(string subjectId, UserCacheEntry entry, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentNullException.ThrowIfNull(entry);

        var key = CacheKey(subjectId);
        await _daprClient.SaveStateAsync(AspireConstants.Resources.DaprStateStore, key, entry, cancellationToken: ct);
    }

    private static string CacheKey(string subjectId) => $"theprey:users:by-subject:{subjectId}";
}
