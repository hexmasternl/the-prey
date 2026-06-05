using System.Diagnostics;

namespace HexMaster.ThePrey.GameEngine.Observability;

internal static class GameEngineActivitySource
{
    internal const string SourceName = "HexMaster.ThePrey.GameEngine";
    internal static readonly ActivitySource Source = new(SourceName);
}
