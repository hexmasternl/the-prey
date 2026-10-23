using System.Diagnostics;

namespace HexMaster.ThePrey.PlayFields.Observability;

internal static class PlayFieldActivitySource
{
    internal const string SourceName = "HexMaster.ThePrey.PlayFields";

    internal static readonly ActivitySource Source = new(SourceName);
}

/// <summary>Exposes constants used for OpenTelemetry registration in the host project.</summary>
public static class PlayFieldObservabilityConstants
{
    public const string ActivitySourceName = PlayFieldActivitySource.SourceName;
    public const string MeterName = PlayFieldMetrics.MeterName;
}
