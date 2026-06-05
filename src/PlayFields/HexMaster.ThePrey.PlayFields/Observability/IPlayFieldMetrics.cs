namespace HexMaster.ThePrey.PlayFields.Observability;

public interface IPlayFieldMetrics
{
    void RecordPlayFieldCreated();

    void RecordPlayFieldDeleted();

    void RecordPublicPlayFieldSearch();
}
