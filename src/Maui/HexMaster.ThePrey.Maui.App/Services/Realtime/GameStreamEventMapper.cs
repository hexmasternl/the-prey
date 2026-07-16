using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Maps a game-channel <c>{ type, data }</c> envelope to a typed <see cref="GameStreamEvent"/>, or
/// <c>null</c> for an event name the gameplay map does not consume. Pure (no I/O) so the mapping is
/// unit-testable without a WebSocket. The <c>data</c> object binds case-insensitively (camelCase) and
/// each reader tolerates the two backend field-name variants for the participant/user id.
/// </summary>
internal static class GameStreamEventMapper
{
    public static GameStreamEvent? Map(string type, JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return null;

        switch (type)
        {
            case "player-location-updated":
            {
                var userId = ReadGuid(data, "userId", "participantId");
                if (userId is null || !TryReadDouble(data, "latitude", out var lat) || !TryReadDouble(data, "longitude", out var lon))
                    return null;
                return new GameStreamEvent.ParticipantLocated(userId.Value, lat, lon, ReadString(data, "participantState", "state"));
            }

            case "participant-status-changed":
            case "player-status-changed":
            {
                var participantId = ReadGuid(data, "participantId", "userId");
                var newState = ReadString(data, "newState", "state");
                if (participantId is null || string.IsNullOrEmpty(newState))
                    return null;
                return new GameStreamEvent.ParticipantStatusChanged(participantId.Value, newState!);
            }

            case "state-changed":
            {
                var newState = ReadString(data, "newState", "state");
                return string.IsNullOrEmpty(newState) ? null : new GameStreamEvent.StateChanged(newState!);
            }

            case "game-ended":
                return new GameStreamEvent.GameEnded(ReadString(data, "outcome"), ReadInt(data, "survivorCount"));

            default:
                return null;
        }
    }

    private static Guid? ReadGuid(JsonElement data, params string[] names)
    {
        foreach (var name in names)
        {
            if (data.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                && Guid.TryParse(el.GetString(), out var guid))
                return guid;
        }
        return null;
    }

    private static bool TryReadDouble(JsonElement data, string name, out double value)
    {
        if (data.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out value))
            return true;
        value = 0;
        return false;
    }

    private static string? ReadString(JsonElement data, params string[] names)
    {
        foreach (var name in names)
        {
            if (data.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString();
        }
        return null;
    }

    private static int? ReadInt(JsonElement data, string name) =>
        data.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var value)
            ? value
            : null;
}
