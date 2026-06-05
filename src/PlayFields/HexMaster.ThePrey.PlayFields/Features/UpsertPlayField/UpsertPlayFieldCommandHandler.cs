using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.PlayFields.Features.UpsertPlayField;

public sealed class UpsertPlayFieldCommandHandler
    : ICommandHandler<UpsertPlayFieldCommand, UpsertPlayFieldResult>
{
    private readonly IPlayFieldRepository _playFields;
    private readonly ILogger<UpsertPlayFieldCommandHandler> _logger;

    public UpsertPlayFieldCommandHandler(
        IPlayFieldRepository playFields,
        ILogger<UpsertPlayFieldCommandHandler> logger)
    {
        _playFields = playFields;
        _logger = logger;
    }

    public async Task<UpsertPlayFieldResult> Handle(UpsertPlayFieldCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = PlayFieldActivitySource.Source.StartActivity("UpsertPlayField");
        activity?.SetTag("playfield.id", command.Id);
        activity?.SetTag("playfield.owner_id", command.OwnerId);

        try
        {
            var points = command.Points
                .Select(p => GpsCoordinate.Create(p.Latitude, p.Longitude))
                .ToList();

            var existing = await _playFields.GetByIdAsync(command.Id, ct);

            if (existing is null)
            {
                var created = PlayField.Create(
                    command.Name,
                    command.OwnerId,
                    points,
                    command.IsPublic,
                    id: command.Id,
                    lastModifiedOn: command.LastUpdatedOn);

                await _playFields.UpsertAsync(created, ct);
                _logger.LogInformation("Play field {PlayFieldId} created via upsert for owner {OwnerId}", created.Id, command.OwnerId);

                return new UpsertPlayFieldResult.Created(created.ToDto());
            }

            if (!string.Equals(existing.OwnerId, command.OwnerId, StringComparison.Ordinal))
                return new UpsertPlayFieldResult.Forbidden();

            if (command.LastUpdatedOn < existing.LastModifiedOn)
                return new UpsertPlayFieldResult.Conflict(existing.ToDto());

            existing.Update(command.Name, command.IsPublic, points, DateTimeOffset.UtcNow);
            await _playFields.UpsertAsync(existing, ct);
            _logger.LogInformation("Play field {PlayFieldId} updated via upsert", existing.Id);

            return new UpsertPlayFieldResult.Updated(existing.ToDto());
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
