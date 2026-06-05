using System.Diagnostics.Metrics;

namespace HexMaster.ThePrey.GameEngine.Observability;

internal interface IGameEngineMetrics
{
    void RecordLocationsBroadcasted(int count, Guid gameId);
    void RecordCycleExecuted(Guid gameId);
}

internal sealed class GameEngineMetrics : IGameEngineMetrics
{
    internal const string MeterName = "HexMaster.ThePrey.GameEngine";

    private readonly Counter<long> _locationsBroadcasted;
    private readonly Counter<long> _cyclesExecuted;

    internal GameEngineMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _locationsBroadcasted = meter.CreateCounter<long>(
            "game_engine.locations_broadcasted",
            unit: "{location}",
            description: "Number of participant locations broadcasted per cycle");

        _cyclesExecuted = meter.CreateCounter<long>(
            "game_engine.cycles_executed",
            unit: "{cycle}",
            description: "Number of location-check cycles executed per game");
    }

    public void RecordLocationsBroadcasted(int count, Guid gameId) =>
        _locationsBroadcasted.Add(count, new KeyValuePair<string, object?>("game.id", gameId));

    public void RecordCycleExecuted(Guid gameId) =>
        _cyclesExecuted.Add(1, new KeyValuePair<string, object?>("game.id", gameId));
}
