using System.Text.Json.Serialization;

namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Minimal client-side projection of the backend <c>GameStatusDto</c> returned by
/// <c>GET /games/active</c>. Only the fields the welcome flow needs are modelled; the
/// backend payload is richer and can be extended here as gameplay screens are built.
/// </summary>
public sealed record GameStatus
{
    [JsonPropertyName("gameId")]
    public Guid GameId { get; init; }

    [JsonPropertyName("playfieldName")]
    public string? PlayfieldName { get; init; }

    [JsonPropertyName("isEndgame")]
    public bool IsEndgame { get; init; }

    [JsonPropertyName("preysLeft")]
    public int PreysLeft { get; init; }
}
