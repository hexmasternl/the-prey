using System.Net;
using HexMaster.ThePrey.Maui.App.Services.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameApiClientTests
{
    private static GameApiClient CreateSut(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://gateway.example.com/")
        };
        return new GameApiClient(http, NullLogger<GameApiClient>.Instance);
    }

    [Fact]
    public async Task GetActiveGameAsync_ShouldReturnHasActiveGame_WhenBackendReturns200()
    {
        var gameId = Guid.NewGuid();
        var json = $$"""{"gameId":"{{gameId}}","playfieldName":"Harbour","isEndgame":false,"preysLeft":3}""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.GetActiveGameAsync("access");

        Assert.Equal(ActiveGameOutcome.HasActiveGame, result.Outcome);
        Assert.NotNull(result.Game);
        Assert.Equal(gameId, result.Game!.GameId);
        Assert.Equal("Harbour", result.Game.PlayfieldName);
    }

    [Fact]
    public async Task GetActiveGameAsync_ShouldSendBearerToken()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.NotFound);
        var sut = CreateSut(handler);

        await sut.GetActiveGameAsync("my-access-token");

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-access-token", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetActiveGameAsync_ShouldReturnNoActiveGame_WhenBackendReturns404()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.NotFound));

        var result = await sut.GetActiveGameAsync("access");

        Assert.Equal(ActiveGameOutcome.NoActiveGame, result.Outcome);
    }

    [Fact]
    public async Task GetActiveGameAsync_ShouldReturnUnauthorized_WhenBackendReturns401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.GetActiveGameAsync("access");

        Assert.Equal(ActiveGameOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task GetActiveGameAsync_ShouldReturnError_WhenBackendReturns500()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.InternalServerError));

        var result = await sut.GetActiveGameAsync("access");

        Assert.Equal(ActiveGameOutcome.Error, result.Outcome);
    }

    // A minimal but complete GameDto JSON (camelCase) the GameDetails projection can bind to.
    private static string GameJson(
        Guid? id = null,
        string gameCode = "1234",
        string status = "Lobby",
        int defaultLocationInterval = 120,
        int finalLocationInterval = 60,
        bool isOwnerPlayer = true,
        bool isReadyToStart = false,
        Guid? hunterUserId = null)
    {
        var gameId = id ?? Guid.NewGuid();
        var hunter = hunterUserId is null ? "null" : $"\"{hunterUserId}\"";
        return $$"""
        {
          "id": "{{gameId}}",
          "gameCode": "{{gameCode}}",
          "playfieldId": "{{Guid.NewGuid()}}",
          "ownerUserId": "{{Guid.NewGuid()}}",
          "status": "{{status}}",
          "configuration": {
            "gameDuration": 30, "hunterDelayTime": 5, "finalStageDuration": 10,
            "defaultLocationInterval": {{defaultLocationInterval}}, "finalLocationInterval": {{finalLocationInterval}},
            "enablePreyBoundaryPenalties": false, "enableHunterBoundaryPenalty": false
          },
          "participants": [
            { "userId": "{{Guid.NewGuid()}}", "displayName": "Alice", "profilePictureUrl": null, "isReady": true, "state": "Lobby", "lastKnownLocation": null, "hasActivePenalty": false }
          ],
          "hunterUserId": {{hunter}},
          "preys": [],
          "startedAt": null, "createdAt": "2026-01-01T00:00:00Z", "endsAt": null,
          "cleanUpAfter": "2026-01-02T00:00:00Z", "outcome": "None", "completedAt": null,
          "isOwnerPlayer": {{isOwnerPlayer.ToString().ToLowerInvariant()}},
          "isReadyToStart": {{isReadyToStart.ToString().ToLowerInvariant()}}
        }
        """;
    }

    // ---- GetGameAsync ----

    [Fact]
    public async Task GetGameAsync_ShouldReturnSuccessWithProjection_WhenBackendReturns200()
    {
        var gameId = Guid.NewGuid();
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, GameJson(gameId, gameCode: "4321")));

        var result = await sut.GetGameAsync(gameId, "access");

        Assert.Equal(GetGameOutcome.Success, result.Outcome);
        Assert.Equal(gameId, result.Game!.Id);
        Assert.Equal("4321", result.Game.GameCode);
        Assert.Single(result.Game.Participants);
    }

    [Fact]
    public async Task GetGameAsync_ShouldSendBearerToken()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.NotFound);
        var sut = CreateSut(handler);

        await sut.GetGameAsync(Guid.NewGuid(), "my-token");

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, GetGameOutcome.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, GetGameOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, GetGameOutcome.Error)]
    public async Task GetGameAsync_ShouldMapStatus(HttpStatusCode status, GetGameOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.GetGameAsync(Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }

    // ---- UpdateGameSettingsAsync ----

    [Fact]
    public async Task UpdateGameSettingsAsync_ShouldConvertPingMinutesToSeconds()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, GameJson());
        var sut = CreateSut(handler);
        var settings = new GameSettingsParameters(
            GameDurationMinutes: 60, HeadstartMinutes: 10, EndgameMinutes: 15,
            PingMinutes: 2, EndgamePingMinutes: 1);

        await sut.UpdateGameSettingsAsync(Guid.NewGuid(), settings, "access");

        Assert.Contains("\"defaultLocationInterval\":120", handler.LastRequestBody);
        Assert.Contains("\"finalLocationInterval\":60", handler.LastRequestBody);
        // Durations pass through unchanged (minutes).
        Assert.Contains("\"gameDuration\":60", handler.LastRequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, UpdateGameSettingsOutcome.Validation)]
    [InlineData(HttpStatusCode.Forbidden, UpdateGameSettingsOutcome.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized, UpdateGameSettingsOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, UpdateGameSettingsOutcome.Error)]
    public async Task UpdateGameSettingsAsync_ShouldMapStatus(HttpStatusCode status, UpdateGameSettingsOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));
        var settings = new GameSettingsParameters(30, 5, 10, 2, 1);

        var result = await sut.UpdateGameSettingsAsync(Guid.NewGuid(), settings, "access");

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task UpdateGameSettingsAsync_ShouldReturnSuccessSnapshot_WhenBackendReturns200()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, GameJson(isReadyToStart: true)));

        var result = await sut.UpdateGameSettingsAsync(Guid.NewGuid(), new GameSettingsParameters(30, 5, 10, 2, 1), "access");

        Assert.Equal(UpdateGameSettingsOutcome.Success, result.Outcome);
        Assert.True(result.Game!.IsReadyToStart);
    }

    // ---- DesignateHunterAsync ----

    [Fact]
    public async Task DesignateHunterAsync_ShouldPostNewHunterId_AndMapSuccess()
    {
        var hunterId = Guid.NewGuid();
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, GameJson(hunterUserId: hunterId));
        var sut = CreateSut(handler);

        var result = await sut.DesignateHunterAsync(Guid.NewGuid(), hunterId, "access");

        Assert.Equal(DesignateHunterOutcome.Success, result.Outcome);
        Assert.Contains($"\"newHunterUserId\":\"{hunterId}\"", handler.LastRequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, DesignateHunterOutcome.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, DesignateHunterOutcome.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, DesignateHunterOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, DesignateHunterOutcome.Error)]
    public async Task DesignateHunterAsync_ShouldMapStatus(HttpStatusCode status, DesignateHunterOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.DesignateHunterAsync(Guid.NewGuid(), Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }

    // ---- SetReadyAsync ----

    [Fact]
    public async Task SetReadyAsync_ShouldMapSuccess_AndSendBearer()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, GameJson());
        var sut = CreateSut(handler);

        var result = await sut.SetReadyAsync(Guid.NewGuid(), "ready-token");

        Assert.Equal(SetReadyOutcome.Success, result.Outcome);
        Assert.Equal("ready-token", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, SetReadyOutcome.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, SetReadyOutcome.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, SetReadyOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, SetReadyOutcome.Error)]
    public async Task SetReadyAsync_ShouldMapStatus(HttpStatusCode status, SetReadyOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.SetReadyAsync(Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }

    // ---- StartGameAsync ----

    [Fact]
    public async Task StartGameAsync_ShouldPostHunterId_AndMapSuccess()
    {
        var hunterId = Guid.NewGuid();
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, GameJson(status: "InProgress"));
        var sut = CreateSut(handler);

        var result = await sut.StartGameAsync(Guid.NewGuid(), hunterId, "access");

        Assert.Equal(StartGameOutcome.Success, result.Outcome);
        Assert.Contains($"\"hunterUserId\":\"{hunterId}\"", handler.LastRequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, StartGameOutcome.Validation)]
    [InlineData(HttpStatusCode.Forbidden, StartGameOutcome.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, StartGameOutcome.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, StartGameOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, StartGameOutcome.Error)]
    public async Task StartGameAsync_ShouldMapStatus(HttpStatusCode status, StartGameOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.StartGameAsync(Guid.NewGuid(), Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }

    // ---- GetNotificationsTokenAsync ----

    [Fact]
    public async Task GetNotificationsTokenAsync_ShouldReturnUrl_WhenBackendReturns200()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, """{"url":"wss://hub.example.com/client?access_token=abc"}""");
        var sut = CreateSut(handler);

        var result = await sut.GetNotificationsTokenAsync(Guid.NewGuid(), "my-token");

        Assert.Equal(NotificationsTokenOutcome.Success, result.Outcome);
        Assert.Equal("wss://hub.example.com/client?access_token=abc", result.Url);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetNotificationsTokenAsync_ShouldReturnError_WhenUrlMissing()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, """{"url":null}"""));

        var result = await sut.GetNotificationsTokenAsync(Guid.NewGuid(), "access");

        Assert.Equal(NotificationsTokenOutcome.Error, result.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, NotificationsTokenOutcome.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized, NotificationsTokenOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, NotificationsTokenOutcome.Error)]
    public async Task GetNotificationsTokenAsync_ShouldMapStatus(HttpStatusCode status, NotificationsTokenOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.GetNotificationsTokenAsync(Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }
}
