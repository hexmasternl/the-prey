using System.Diagnostics.Metrics;

namespace HexMaster.ThePrey.Games.Observability;

public class GameMetrics : IGameMetrics
{
    internal const string MeterName = "HexMaster.ThePrey.Games";

    private readonly Counter<long> _gamesCreated;
    private readonly Counter<long> _gamesStarted;
    private readonly Counter<long> _locationsRecorded;
    private readonly Counter<long> _gamesCompleted;

    public GameMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _gamesCreated = meter.CreateCounter<long>(
            "games.created",
            unit: "{game}",
            description: "Total number of games created");

        _gamesStarted = meter.CreateCounter<long>(
            "games.started",
            unit: "{game}",
            description: "Total number of games started");

        _locationsRecorded = meter.CreateCounter<long>(
            "games.locations_recorded",
            unit: "{location}",
            description: "Total number of player locations recorded");

        _gamesCompleted = meter.CreateCounter<long>(
            "games.completed",
            unit: "{game}",
            description: "Total number of games completed");
    }

    public virtual void RecordGameCreated() => _gamesCreated.Add(1);

    public virtual void RecordGameStarted() => _gamesStarted.Add(1);

    public virtual void RecordLocationRecorded() => _locationsRecorded.Add(1);

    public virtual void RecordGameCompleted(string outcome) =>
        _gamesCompleted.Add(1, new KeyValuePair<string, object?>("game.outcome", outcome));
}
