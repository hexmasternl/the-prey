using System.Text.Json;
using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

public sealed class PlayfieldCacheService
{
    private static string CacheFilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "playfields.json");

    public async Task<IReadOnlyList<Playfield>> LoadAsync()
    {
        if (!File.Exists(CacheFilePath))
            return [];
        try
        {
            await using var stream = File.OpenRead(CacheFilePath);
            return await JsonSerializer.DeserializeAsync<List<Playfield>>(stream) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task SaveAsync(IEnumerable<Playfield> playfields)
    {
        await using var stream = File.Create(CacheFilePath);
        await JsonSerializer.SerializeAsync(stream, playfields);
    }

    public async Task RemoveAsync(string id)
    {
        var current = (await LoadAsync()).ToList();
        current.RemoveAll(p => p.Id == id);
        await SaveAsync(current);
    }
}
