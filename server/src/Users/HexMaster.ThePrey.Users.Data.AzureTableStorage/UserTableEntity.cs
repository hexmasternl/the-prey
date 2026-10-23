using Azure;
using Azure.Data.Tables;

namespace HexMaster.ThePrey.Users.Data.AzureTableStorage;

/// <summary>
/// Table Storage representation of a user.
/// <see cref="ITableEntity.PartitionKey"/> holds the SubjectId;
/// <see cref="ITableEntity.RowKey"/> is always the literal "user" (one row per user, fast point read).
/// </summary>
internal sealed class UserTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = RowKeyValue;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>The fixed row key for all user entities.</summary>
    public const string RowKeyValue = "user";

    public string Id { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Callsign { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public string PreferredLanguage { get; set; } = string.Empty;
}
