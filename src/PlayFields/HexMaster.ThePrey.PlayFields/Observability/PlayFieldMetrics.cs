using System.Diagnostics.Metrics;

namespace HexMaster.ThePrey.PlayFields.Observability;

public class PlayFieldMetrics : IPlayFieldMetrics
{
    internal const string MeterName = "HexMaster.ThePrey.PlayFields";

    private readonly Counter<long> _playFieldsCreated;

    public PlayFieldMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _playFieldsCreated = meter.CreateCounter<long>(
            "playfields.created",
            unit: "{playfield}",
            description: "Total number of play fields created");
    }

    public virtual void RecordPlayFieldCreated() => _playFieldsCreated.Add(1);
}
