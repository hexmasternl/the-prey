using System.Diagnostics.Metrics;

namespace HexMaster.ThePrey.PlayFields.Observability;

public class PlayFieldMetrics : IPlayFieldMetrics
{
    internal const string MeterName = "HexMaster.ThePrey.PlayFields";

    private readonly Counter<long> _playFieldsCreated;
    private readonly Counter<long> _playFieldsDeleted;
    private readonly Counter<long> _publicPlayFieldSearches;

    public PlayFieldMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _playFieldsCreated = meter.CreateCounter<long>(
            "playfields.created",
            unit: "{playfield}",
            description: "Total number of play fields created");

        _playFieldsDeleted = meter.CreateCounter<long>(
            "playfields.deleted",
            unit: "{playfield}",
            description: "Total number of play fields deleted");

        _publicPlayFieldSearches = meter.CreateCounter<long>(
            "playfields.public_searches",
            unit: "{search}",
            description: "Total number of public play-field searches executed");
    }

    public virtual void RecordPlayFieldCreated() => _playFieldsCreated.Add(1);

    public virtual void RecordPlayFieldDeleted() => _playFieldsDeleted.Add(1);

    public virtual void RecordPublicPlayFieldSearch(string mode) =>
        _publicPlayFieldSearches.Add(1, new KeyValuePair<string, object?>("playfield.search.mode", mode));
}
