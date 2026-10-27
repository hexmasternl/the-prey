namespace HexMaster.ThePrey.PlayFields.Observability;

public interface IPlayFieldMetrics
{
    void RecordPlayFieldCreated();

    void RecordPlayFieldDeleted();

    /// <summary>Counts a public search; <paramref name="mode"/> is <c>name</c>, <c>near</c> or <c>both</c> (never coordinates or text).</summary>
    void RecordPublicPlayFieldSearch(string mode);
}
