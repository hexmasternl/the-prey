using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

public sealed class PlayfieldSyncService(
    IPlayfieldService playfieldService,
    PlayfieldCacheService cache)
{
    /// <returns><c>true</c> if the remote pull succeeded; <c>false</c> if it failed (cached data is still shown).</returns>
    public async Task<bool> SyncAsync(CancellationToken ct = default)
    {
        if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            return false;

        var allCached = (await cache.LoadAsync()).ToList();

        // Push phase: upload every unsynced playfield.
        foreach (var local in allCached.Where(p => !p.IsSynchronized).ToList())
        {
            try
            {
                var synced = await playfieldService.UpsertPlayfieldAsync(local, ct);
                synced.IsSynchronized = true;
                var idx = allCached.FindIndex(p => p.Id == local.Id);
                if (idx >= 0) allCached[idx] = synced;
            }
            catch (StaleWriteException ex)
            {
                // Server has a newer copy — adopt it and mark synced.
                var serverCopy = ex.ServerCopy;
                serverCopy.IsSynchronized = true;
                var idx = allCached.FindIndex(p => p.Id == local.Id);
                if (idx >= 0) allCached[idx] = serverCopy;
            }
            catch
            {
                // Network or server error — leave unsynced for retry on next pass.
            }
        }

        // Pull phase: fetch server list and merge per LWW rule.
        bool pullSucceeded;
        try
        {
            var serverList = await playfieldService.GetPlayfieldsAsync(ct);
            foreach (var server in serverList)
            {
                var localIdx = allCached.FindIndex(p => p.Id == server.Id);
                if (localIdx < 0)
                {
                    server.IsSynchronized = true;
                    allCached.Add(server);
                }
                else if (server.LastUpdatedOn > allCached[localIdx].LastUpdatedOn)
                {
                    server.IsSynchronized = true;
                    allCached[localIdx] = server;
                }
                // else: local copy is newer or same — keep.
            }
            pullSucceeded = true;
        }
        catch
        {
            pullSucceeded = false;
        }

        await cache.SaveAsync(allCached);
        return pullSucceeded;
    }
}
