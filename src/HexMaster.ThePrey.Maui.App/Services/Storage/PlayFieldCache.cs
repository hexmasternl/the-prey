using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Storage;

/// <summary>
/// <see cref="IPlayFieldCache"/> backed by a single JSON file in an app-owned directory (the MAUI
/// registration passes <c>FileSystem.AppDataDirectory</c>). Plain .NET — the directory is injected
/// rather than read from a MAUI API — so the real implementation is unit-testable against a temp
/// directory. <see cref="SaveAsync"/> overwrites the whole file; <see cref="LoadAsync"/> treats a
/// missing or unparseable file as "no cache" and never throws.
/// </summary>
public sealed class PlayFieldCache : IPlayFieldCache
{
    private const string FileName = "private-playfields.json";

    private readonly string _filePath;
    private readonly ILogger<PlayFieldCache> _logger;

    public PlayFieldCache(string directoryPath, ILogger<PlayFieldCache> logger)
    {
        _filePath = Path.Combine(directoryPath, FileName);
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlayFieldSummary>> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_filePath))
                return [];

            await using var stream = File.OpenRead(_filePath);
            var items = await JsonSerializer.DeserializeAsync<PlayFieldSummary[]>(stream, cancellationToken: ct);
            return items ?? [];
        }
        catch (Exception ex)
        {
            // A missing, corrupt, or unreadable file degrades to "no cache" — the online path takes over.
            _logger.LogWarning(ex, "Failed to read the private-playfields cache; treating it as empty.");
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyList<PlayFieldSummary> items, CancellationToken ct = default)
    {
        try
        {
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, items, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // A failed write is non-fatal: the list still shows, the next successful refresh retries.
            _logger.LogWarning(ex, "Failed to write the private-playfields cache.");
        }
    }
}
