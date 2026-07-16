using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Realtime;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameStreamEventMapperTests
{
    private static JsonElement Data(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Map_PlayerLocationUpdated_ProducesParticipantLocated()
    {
        var userId = Guid.NewGuid();
        var evt = GameStreamEventMapper.Map("player-location-updated",
            Data($$"""{"gameId":"{{Guid.NewGuid()}}","userId":"{{userId}}","latitude":52.1,"longitude":4.2,"participantState":"Active"}"""));

        var located = Assert.IsType<GameStreamEvent.ParticipantLocated>(evt);
        Assert.Equal(userId, located.UserId);
        Assert.Equal(52.1, located.Latitude, 5);
        Assert.Equal(4.2, located.Longitude, 5);
        Assert.Equal("Active", located.State);
    }

    [Fact]
    public void Map_ParticipantStatusChanged_UsesParticipantId()
    {
        var participantId = Guid.NewGuid();
        var evt = GameStreamEventMapper.Map("participant-status-changed",
            Data($$"""{"gameId":"{{Guid.NewGuid()}}","participantId":"{{participantId}}","participantRole":"Prey","newState":"Tagged"}"""));

        var status = Assert.IsType<GameStreamEvent.ParticipantStatusChanged>(evt);
        Assert.Equal(participantId, status.ParticipantId);
        Assert.Equal("Tagged", status.NewState);
    }

    [Fact]
    public void Map_PlayerStatusChanged_FallsBackToUserId()
    {
        var userId = Guid.NewGuid();
        var evt = GameStreamEventMapper.Map("player-status-changed",
            Data($$"""{"gameId":"{{Guid.NewGuid()}}","userId":"{{userId}}","role":"Prey","newState":"Out"}"""));

        var status = Assert.IsType<GameStreamEvent.ParticipantStatusChanged>(evt);
        Assert.Equal(userId, status.ParticipantId);
        Assert.Equal("Out", status.NewState);
    }

    [Fact]
    public void Map_StateChanged_ProducesStateChanged()
    {
        var evt = GameStreamEventMapper.Map("state-changed",
            Data($$"""{"gameId":"{{Guid.NewGuid()}}","newState":"InProgress"}"""));

        Assert.Equal("InProgress", Assert.IsType<GameStreamEvent.StateChanged>(evt).NewState);
    }

    [Fact]
    public void Map_GameEnded_ProducesGameEnded()
    {
        var evt = GameStreamEventMapper.Map("game-ended",
            Data($$"""{"gameId":"{{Guid.NewGuid()}}","outcome":"HunterWins","survivorCount":0}"""));

        var ended = Assert.IsType<GameStreamEvent.GameEnded>(evt);
        Assert.Equal("HunterWins", ended.Outcome);
        Assert.Equal(0, ended.SurvivorCount);
    }

    [Fact]
    public void Map_UnknownType_ReturnsNull() =>
        Assert.Null(GameStreamEventMapper.Map("player-penalized", Data("""{"gameId":"x"}""")));

    [Fact]
    public void Map_LocationWithoutCoordinates_ReturnsNull() =>
        Assert.Null(GameStreamEventMapper.Map("player-location-updated", Data($$"""{"userId":"{{Guid.NewGuid()}}"}""")));
}
