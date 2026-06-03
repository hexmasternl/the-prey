using Azure;
using Azure.Data.Tables;

namespace HexMaster.ThePrey.PlayFields.Data.TableStorage;

/// <summary>
/// Table Storage representation of a play field. <see cref="ITableEntity.PartitionKey"/> holds the owner id
/// (so a player's fields share a partition) and <see cref="ITableEntity.RowKey"/> holds the play field id.
/// The ordered points are stored as a JSON string because Table Storage has no native collection column.
/// </summary>
internal sealed class PlayFieldTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public string PointsJson { get; set; } = "[]";
    public DateTimeOffset LastModifiedOn { get; set; }
    public double? CenterLatitude { get; set; }
    public double? CenterLongitude { get; set; }
}
