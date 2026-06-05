using HexMaster.ThePrey.PlayFields.DomainModels;

namespace HexMaster.ThePrey.PlayFields;

public interface IPlayFieldRepository
{
    Task AddAsync(PlayField playField, CancellationToken ct);

    Task UpsertAsync(PlayField playField, CancellationToken ct);

    Task<PlayField?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Returns only the play fields owned by the specified player.</summary>
    Task<IReadOnlyList<PlayField>> ListByOwnerAsync(Guid ownerId, CancellationToken ct);

    Task DeleteAsync(Guid id, Guid ownerId, CancellationToken ct);

    /// <summary>Returns the public play fields whose name contains the search text (case-insensitive).</summary>
    Task<IReadOnlyList<PlayField>> SearchPublicAsync(string searchText, CancellationToken ct);
}
