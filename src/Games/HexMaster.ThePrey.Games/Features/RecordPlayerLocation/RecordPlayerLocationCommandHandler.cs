using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.RecordPlayerLocation;

public sealed class RecordPlayerLocationCommandHandler : ICommandHandler<RecordPlayerLocationCommand, RecordPlayerLocationResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public RecordPlayerLocationCommandHandler(
        IGameRepository games,
        IGameMetrics metrics,
        TimeProvider timeProvider)
    {
        _games = games;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public async Task<RecordPlayerLocationResult?> Handle(RecordPlayerLocationCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _games.GetByIdAsync(command.GameId, ct);
        if (game is null)
            return null;

        using var activity = GameActivitySource.Source.StartActivity("RecordPlayerLocation");
        activity?.SetTag("game.id", game.Id);
        activity?.SetTag("game.user_id", command.UserId);

        var now = _timeProvider.GetUtcNow();
        var recordedAt = command.RecordedAt ?? now;
        var coordinate = GpsCoordinate.Create(command.Latitude, command.Longitude);

        game.RecordLocation(command.UserId, coordinate, recordedAt);

        await _games.UpdateAsync(game, ct);

        _metrics.RecordLocationRecorded();

        var nextInterval = game.ReportingIntervalFor(command.UserId, now);
        return new RecordPlayerLocationResult(new RecordLocationResponse(true, nextInterval));
    }
}
