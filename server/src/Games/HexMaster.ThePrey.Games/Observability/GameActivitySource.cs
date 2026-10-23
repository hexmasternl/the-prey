using System.Diagnostics;

namespace HexMaster.ThePrey.Games.Observability;

internal static class GameActivitySource
{
    internal const string SourceName = "HexMaster.ThePrey.Games";

    internal static readonly ActivitySource Source = new(SourceName);
}

/// <summary>Exposes constants used for OpenTelemetry registration in the host project.</summary>
public static class GameObservabilityConstants
{
    public const string ActivitySourceName = GameActivitySource.SourceName;
    public const string MeterName = GameMetrics.MeterName;
}
