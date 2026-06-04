using Azure;
using Azure.Data.Tables;
using HexMaster.ThePrey.Users.DomainModels;

namespace HexMaster.ThePrey.Users.Data.AzureTableStorage;

public sealed class AzureTableStorageUserRepository : IUserRepository
{
    internal const string TableName = "users";

    private readonly TableServiceClient _serviceClient;

    public AzureTableStorageUserRepository(TableServiceClient serviceClient) =>
        _serviceClient = serviceClient;

    public async Task<User?> GetBySubjectIdAsync(string subjectId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var table = await GetTableClientAsync(ct);

        try
        {
            var response = await table.GetEntityAsync<UserTableEntity>(
                subjectId, UserTableEntity.RowKeyValue, cancellationToken: ct);
            return ToDomain(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        var table = await GetTableClientAsync(ct);
        await table.AddEntityAsync(ToEntity(user), ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        var table = await GetTableClientAsync(ct);
        await table.UpsertEntityAsync(ToEntity(user), TableUpdateMode.Replace, ct);
    }

    private async Task<TableClient> GetTableClientAsync(CancellationToken ct)
    {
        var table = _serviceClient.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct);
        return table;
    }

    private static UserTableEntity ToEntity(User user) =>
        new()
        {
            PartitionKey = user.SubjectId,
            RowKey = UserTableEntity.RowKeyValue,
            Id = user.Id.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            Callsign = user.Callsign,
            EmailAddress = user.EmailAddress,
            IsEmailVerified = user.IsEmailVerified,
            PreferredLanguage = user.PreferredLanguage
        };

    private static User ToDomain(UserTableEntity entity) =>
        User.Rehydrate(
            Guid.Parse(entity.Id),
            entity.PartitionKey,
            entity.FirstName,
            entity.LastName,
            entity.DisplayName,
            entity.Callsign,
            entity.EmailAddress,
            entity.IsEmailVerified,
            entity.PreferredLanguage);
}
