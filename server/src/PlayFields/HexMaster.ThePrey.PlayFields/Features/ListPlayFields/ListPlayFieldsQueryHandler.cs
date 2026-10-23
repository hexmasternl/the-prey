using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.PlayFields.Features.ListPlayFields;

public sealed class ListPlayFieldsQueryHandler : IQueryHandler<ListPlayFieldsQuery, IReadOnlyList<PlayFieldSummaryDto>>
{
    private readonly IPlayFieldRepository _playFields;

    public ListPlayFieldsQueryHandler(IPlayFieldRepository playFields) => _playFields = playFields;

    public async Task<IReadOnlyList<PlayFieldSummaryDto>> Handle(ListPlayFieldsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var playFields = await _playFields.ListByOwnerAsync(query.RequestingOwnerId, ct);

        return playFields.Select(p => p.ToSummaryDto()).ToList();
    }
}
