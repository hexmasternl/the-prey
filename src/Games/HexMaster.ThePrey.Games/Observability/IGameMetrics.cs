namespace HexMaster.ThePrey.Games.Observability;

public interface IGameMetrics
{
    void RecordGameCreated();

    void RecordGameStarted();

    void RecordLocationRecorded();

    void RecordGameCompleted(string outcome);
}
