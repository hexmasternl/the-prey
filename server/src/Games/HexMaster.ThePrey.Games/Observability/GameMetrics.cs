using System.Diagnostics.Metrics;

namespace HexMaster.ThePrey.Games.Observability;

public class GameMetrics : IGameMetrics
{
    internal const string MeterName = "HexMaster.ThePrey.Games";

    private readonly Counter<long> _gamesCreated;
    private readonly Counter<long> _gamesStarted;
    private readonly Counter<long> _locationsRecorded;
    private readonly Counter<long> _gamesCompleted;
    private readonly Histogram<double> _sweepDuration;
    private readonly Counter<long> _broadcasts;
    private readonly Counter<long> _penaltiesApplied;
    private readonly Counter<long> _leadershipChanges;
    private readonly Counter<long> _sweepOverruns;

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

        _sweepDuration = meter.CreateHistogram<double>(
            "games.sweep.duration",
            unit: "ms",
            description: "Duration of a game sweep tick");

        _broadcasts = meter.CreateCounter<long>(
            "games.sweep.broadcasts",
            unit: "{broadcast}",
            description: "Total number of last-known-position broadcasts emitted by the sweep");

        _penaltiesApplied = meter.CreateCounter<long>(
            "games.sweep.penalties",
            unit: "{penalty}",
            description: "Total number of boundary penalties applied by the sweep");

        _leadershipChanges = meter.CreateCounter<long>(
            "games.sweep.leadership_changes",
            unit: "{change}",
            description: "Total number of sweep leadership acquisitions/losses on this replica");

        _sweepOverruns = meter.CreateCounter<long>(
            "games.sweep.overruns",
            unit: "{tick}",
            description: "Total number of sweep ticks that overran their interval");
    }

    public virtual void RecordGameCreated() => _gamesCreated.Add(1);

    public virtual void RecordGameStarted() => _gamesStarted.Add(1);

    public virtual void RecordLocationRecorded() => _locationsRecorded.Add(1);

    public virtual void RecordGameCompleted(string outcome) =>
        _gamesCompleted.Add(1, new KeyValuePair<string, object?>("game.outcome", outcome));

    public virtual void RecordSweepTick(int gameCount, double durationMs) =>
        _sweepDuration.Record(durationMs, new KeyValuePair<string, object?>("games.count", gameCount));

    public virtual void RecordBroadcasts(int count)
    {
        if (count > 0) _broadcasts.Add(count);
    }

    public virtual void RecordPenaltiesApplied(int count)
    {
        if (count > 0) _penaltiesApplied.Add(count);
    }

    public virtual void RecordLeadershipChanged(bool acquired) =>
        _leadershipChanges.Add(1, new KeyValuePair<string, object?>("leader.acquired", acquired));

    public virtual void RecordSweepOverrun() => _sweepOverruns.Add(1);
}
