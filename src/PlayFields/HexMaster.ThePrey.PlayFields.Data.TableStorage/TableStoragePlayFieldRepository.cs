using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using HexMaster.ThePrey.PlayFields.DomainModels;

namespace HexMaster.ThePrey.PlayFields.Data.TableStorage;

public sealed class TableStoragePlayFieldRepository : IPlayFieldRepository
{
    internal const string TableName = "playfields";

    private readonly TableServiceClient _serviceClient;

    public TableStoragePlayFieldRepository(TableServiceClient serviceClient) => _serviceClient = serviceClient;

    public async Task AddAsync(PlayField playField, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(playField);

        var table = await GetTableClientAsync(ct);
        await table.AddEntityAsync(ToEntity(playField), ct);
    }

    public async Task UpsertAsync(PlayField playField, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(playField);

        var table = await GetTableClientAsync(ct);
        await table.UpsertEntityAsync(ToEntity(playField), TableUpdateMode.Replace, ct);
    }

    public async Task<PlayField?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var table = await GetTableClientAsync(ct);

        // The owner partition is unknown for a point lookup, so query by RowKey across partitions.
        var rowKey = id.ToString();
        var query = table.QueryAsync<PlayFieldTableEntity>(e => e.RowKey == rowKey, cancellationToken: ct);

        await foreach (var entity in query)
            return ToDomain(entity);

        return null;
    }

    public async Task<IReadOnlyList<PlayField>> ListVisibleToAsync(string ownerId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var table = await GetTableClientAsync(ct);

        var results = new List<PlayField>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // The caller's own play fields (single partition).
        var owned = table.QueryAsync<PlayFieldTableEntity>(e => e.PartitionKey == ownerId, cancellationToken: ct);
        await foreach (var entity in owned)
        {
            if (seen.Add(entity.RowKey))
                results.Add(ToDomain(entity));
        }

        // Public play fields owned by anyone else (cross-partition scan).
        var publicFields = table.QueryAsync<PlayFieldTableEntity>(
            e => e.IsPublic && e.PartitionKey != ownerId, cancellationToken: ct);
        await foreach (var entity in publicFields)
        {
            if (seen.Add(entity.RowKey))
                results.Add(ToDomain(entity));
        }

        return results;
    }

    private async Task<TableClient> GetTableClientAsync(CancellationToken ct)
    {
        var table = _serviceClient.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct);
        return table;
    }

    private static PlayFieldTableEntity ToEntity(PlayField playField)
    {
        var points = playField.Points.Select(p => new StoredPoint(p.Latitude, p.Longitude)).ToList();
        return new PlayFieldTableEntity
        {
            PartitionKey = playField.OwnerId,
            RowKey = playField.Id.ToString(),
            Name = playField.Name,
            IsPublic = playField.IsPublic,
            PointsJson = JsonSerializer.Serialize(points),
            LastModifiedOn = playField.LastModifiedOn,
            CenterLatitude = playField.CenterCoordinates?.Latitude,
            CenterLongitude = playField.CenterCoordinates?.Longitude
        };
    }

    private static PlayField ToDomain(PlayFieldTableEntity entity)
    {
        var stored = JsonSerializer.Deserialize<List<StoredPoint>>(entity.PointsJson) ?? [];
        var points = stored.Select(p => GpsCoordinate.Create(p.Latitude, p.Longitude)).ToList();

        var lastModifiedOn = entity.LastModifiedOn == default
            ? DateTimeOffset.MinValue
            : entity.LastModifiedOn;

        GpsCoordinate? center = entity.CenterLatitude.HasValue && entity.CenterLongitude.HasValue
            ? new GpsCoordinate(entity.CenterLatitude.Value, entity.CenterLongitude.Value)
            : null;

        return PlayField.Rehydrate(
            Guid.Parse(entity.RowKey),
            entity.Name,
            entity.PartitionKey,
            entity.IsPublic,
            points,
            lastModifiedOn,
            center);
    }

    private sealed record StoredPoint(double Latitude, double Longitude);
}
