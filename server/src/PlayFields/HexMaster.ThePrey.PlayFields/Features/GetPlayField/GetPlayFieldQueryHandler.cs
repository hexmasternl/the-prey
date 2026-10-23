using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.PlayFields.Features.GetPlayField;

public sealed class GetPlayFieldQueryHandler : IQueryHandler<GetPlayFieldQuery, PlayFieldDto?>
{
    private readonly IPlayFieldRepository _playFields;

    public GetPlayFieldQueryHandler(IPlayFieldRepository playFields) => _playFields = playFields;

    public async Task<PlayFieldDto?> Handle(GetPlayFieldQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var playField = await _playFields.GetByIdAsync(query.PlayFieldId, ct);
        if (playField is null)
            return null;

        // Visibility: only the owner may see a private play field.
        if (!playField.IsPublic && playField.OwnerId != query.RequestingOwnerId)
            return null;

        return playField.ToDto();
    }
}
