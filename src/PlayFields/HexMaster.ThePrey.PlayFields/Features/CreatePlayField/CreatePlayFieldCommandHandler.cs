using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.PlayFields.Features.CreatePlayField;

public sealed class CreatePlayFieldCommandHandler : ICommandHandler<CreatePlayFieldCommand, CreatePlayFieldResult>
{
    private readonly IPlayFieldRepository _playFields;
    private readonly IPlayFieldMetrics _metrics;
    private readonly ILogger<CreatePlayFieldCommandHandler> _logger;

    public CreatePlayFieldCommandHandler(
        IPlayFieldRepository playFields,
        IPlayFieldMetrics metrics,
        ILogger<CreatePlayFieldCommandHandler> logger)
    {
        _playFields = playFields;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<CreatePlayFieldResult> Handle(CreatePlayFieldCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = PlayFieldActivitySource.Source.StartActivity("CreatePlayField");
        activity?.SetTag("playfield.owner_id", command.OwnerId);

        try
        {
            var points = command.Points
                .Select(p => GpsCoordinate.Create(p.Latitude, p.Longitude))
                .ToList();

            var playField = PlayField.Create(command.Name, command.OwnerId, points, command.IsPublic);

            await _playFields.AddAsync(playField, ct);

            _metrics.RecordPlayFieldCreated();
            _logger.LogInformation("Play field {PlayFieldId} created for owner {OwnerId}", playField.Id, command.OwnerId);

            activity?.SetTag("playfield.id", playField.Id);

            return new CreatePlayFieldResult(playField.ToDto());
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
