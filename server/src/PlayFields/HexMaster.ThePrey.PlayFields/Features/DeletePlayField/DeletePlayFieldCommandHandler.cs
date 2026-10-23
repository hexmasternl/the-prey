using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.PlayFields.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.PlayFields.Features.DeletePlayField;

public sealed class DeletePlayFieldCommandHandler : ICommandHandler<DeletePlayFieldCommand, DeletePlayFieldResult>
{
    private readonly IPlayFieldRepository _playFields;
    private readonly IPlayFieldMetrics _metrics;
    private readonly ILogger<DeletePlayFieldCommandHandler> _logger;

    public DeletePlayFieldCommandHandler(
        IPlayFieldRepository playFields,
        IPlayFieldMetrics metrics,
        ILogger<DeletePlayFieldCommandHandler> logger)
    {
        _playFields = playFields;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<DeletePlayFieldResult> Handle(DeletePlayFieldCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = PlayFieldActivitySource.Source.StartActivity("DeletePlayField");
        activity?.SetTag("playfield.id", command.PlayFieldId);

        try
        {
            var playField = await _playFields.GetByIdAsync(command.PlayFieldId, ct);
            if (playField is null)
            {
                _logger.LogWarning("Play field {PlayFieldId} not found", command.PlayFieldId);
                return new DeletePlayFieldResult.NotFound();
            }

            if (playField.OwnerId != command.OwnerId)
            {
                _logger.LogWarning("Caller is not the owner of play field {PlayFieldId}", command.PlayFieldId);
                return new DeletePlayFieldResult.Forbidden();
            }

            await _playFields.DeleteAsync(command.PlayFieldId, command.OwnerId, ct);

            _metrics.RecordPlayFieldDeleted();
            _logger.LogInformation("Play field {PlayFieldId} deleted by owner", command.PlayFieldId);

            return new DeletePlayFieldResult.Success();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
