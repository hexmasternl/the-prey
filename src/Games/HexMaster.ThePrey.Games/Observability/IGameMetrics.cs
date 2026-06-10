namespace HexMaster.ThePrey.Games.Observability;

public interface IGameMetrics
{
    void RecordGameCreated();

    void RecordGameStarted();

    void RecordLocationRecorded();

    void RecordGameCompleted(string outcome);

    /// <summary>Records one completed sweep tick: the number of in-progress games scanned and its duration.</summary>
    void RecordSweepTick(int gameCount, double durationMs);

    /// <summary>Records the number of broadcast ("last known position") updates emitted in a tick.</summary>
    void RecordBroadcasts(int count);

    /// <summary>Records the number of boundary penalties applied in a tick.</summary>
    void RecordPenaltiesApplied(int count);

    /// <summary>Records a change in this replica's sweep leadership.</summary>
    void RecordLeadershipChanged(bool acquired);

    /// <summary>Records that a sweep tick took longer than its interval (overran).</summary>
    void RecordSweepOverrun();
}
