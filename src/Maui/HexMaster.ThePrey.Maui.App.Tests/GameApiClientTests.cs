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

    // ---- CreateGameAsync ----

    private static CreateGameParameters CreateParams(Guid? playfieldId = null) => new(
        PlayfieldId: playfieldId ?? Guid.NewGuid(),
        DisplayName: "Alice",
        GameDurationMinutes: 60,
        HeadstartMinutes: 10,
        EndgameMinutes: 15,
        DefaultLocationIntervalSeconds: 120,
        FinalLocationIntervalSeconds: 60);

    [Fact]
    public async Task CreateGameAsync_ShouldPostToGames_WithBearerAndContractBody()
    {
        var playfieldId = Guid.NewGuid();
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.Created, GameJson());
        var sut = CreateSut(handler);

        await sut.CreateGameAsync(CreateParams(playfieldId), "create-token");

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("games", handler.LastRequest?.RequestUri?.AbsolutePath.TrimStart('/'));
        Assert.Equal("create-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Contains($"\"playfieldId\":\"{playfieldId}\"", handler.LastRequestBody);
        Assert.Contains("\"displayName\":\"Alice\"", handler.LastRequestBody);
        // Durations pass through in minutes; the two intervals are sent verbatim (already seconds).
        Assert.Contains("\"gameDuration\":60", handler.LastRequestBody);
        Assert.Contains("\"defaultLocationInterval\":120", handler.LastRequestBody);
        Assert.Contains("\"finalLocationInterval\":60", handler.LastRequestBody);
        // Boundary-penalty toggles and profile-picture url are sent as their contract defaults.
        Assert.Contains("\"enablePreyBoundaryPenalties\":false", handler.LastRequestBody);
        Assert.Contains("\"enableHunterBoundaryPenalty\":false", handler.LastRequestBody);
        Assert.Contains("\"profilePictureUrl\":null", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateGameAsync_ShouldReturnSuccessWithId_WhenBackendReturns201()
    {
        var gameId = Guid.NewGuid();
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Created, GameJson(gameId)));

        var result = await sut.CreateGameAsync(CreateParams(), "access");

        Assert.Equal(CreateGameOutcome.Success, result.Outcome);
        Assert.Equal(gameId, result.Game!.Id);
    }

    [Fact]
    public async Task CreateGameAsync_ShouldReturnError_WhenCreatedPayloadHasNoId()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Created, """{"gameCode":"1234"}"""));

        var result = await sut.CreateGameAsync(CreateParams(), "access");

        Assert.Equal(CreateGameOutcome.Error, result.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, CreateGameOutcome.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, CreateGameOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, CreateGameOutcome.Error)]
    public async Task CreateGameAsync_ShouldMapStatus(HttpStatusCode status, CreateGameOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.CreateGameAsync(CreateParams(), "access");

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task CreateGameAsync_ShouldReturnError_WhenRequestThrows()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.CreateGameAsync(CreateParams(), "access");

        Assert.Equal(CreateGameOutcome.Error, result.Outcome);
    }

    // ---- JoinGameAsync ----

    [Fact]
    public async Task JoinGameAsync_ShouldPostToJoin_WithBearerAndBody()
    {
        var gameId = Guid.NewGuid();
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, GameJson(gameId));
        var sut = CreateSut(handler);

        await sut.JoinGameAsync(gameId, "1234", "Alice", "join-token");

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal($"games/{gameId}/join", handler.LastRequest?.RequestUri?.AbsolutePath.TrimStart('/'));
        Assert.Equal("join-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Contains("\"joinCode\":\"1234\"", handler.LastRequestBody);
        Assert.Contains("\"displayName\":\"Alice\"", handler.LastRequestBody);
        Assert.Contains("\"profilePictureUrl\":null", handler.LastRequestBody);
    }

    [Fact]
    public async Task JoinGameAsync_ShouldReturnSuccessWithId_WhenBackendReturns200()
    {
        var gameId = Guid.NewGuid();
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, GameJson(gameId)));

        var result = await sut.JoinGameAsync(gameId, "1234", "Alice", "access");

        Assert.Equal(JoinGameOutcome.Success, result.Outcome);
        Assert.Equal(gameId, result.Game!.Id);
    }

    [Fact]
    public async Task JoinGameAsync_ShouldReturnError_WhenOkPayloadHasNoId()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, """{"gameCode":"1234"}"""));

        var result = await sut.JoinGameAsync(Guid.NewGuid(), "1234", "Alice", "access");

        Assert.Equal(JoinGameOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task JoinGameAsync_ShouldReturnInvalidCode_WithProblemCode_WhenBackendReturns400()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.BadRequest, """{"code":"invalid_join_code"}"""));

        var result = await sut.JoinGameAsync(Guid.NewGuid(), "9999", "Alice", "access");

        Assert.Equal(JoinGameOutcome.InvalidCode, result.Outcome);
        Assert.Equal("invalid_join_code", result.Code);
    }

    [Fact]
    public async Task JoinGameAsync_ShouldReturnConflict_WithProblemCode_WhenBackendReturns409()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Conflict, """{"code":"game_already_started"}"""));

        var result = await sut.JoinGameAsync(Guid.NewGuid(), "1234", "Alice", "access");

        Assert.Equal(JoinGameOutcome.Conflict, result.Outcome);
        Assert.Equal("game_already_started", result.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, JoinGameOutcome.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, JoinGameOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, JoinGameOutcome.Error)]
    public async Task JoinGameAsync_ShouldMapStatus(HttpStatusCode status, JoinGameOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.JoinGameAsync(Guid.NewGuid(), "1234", "Alice", "access");

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task JoinGameAsync_ShouldReturnError_WhenRequestThrows()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.JoinGameAsync(Guid.NewGuid(), "1234", "Alice", "access");

        Assert.Equal(JoinGameOutcome.Error, result.Outcome);
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

    // ---- GetGameStatusDetailsAsync (rich map snapshot) ----

    // A minimal in-progress GameStatusDto (camelCase) the GameStatusDetails projection can bind to.
    private static string StatusJson(Guid? hunter = null, Guid? prey = null)
    {
        var hunterId = hunter ?? Guid.NewGuid();
        var preyId = prey ?? Guid.NewGuid();
        return $$"""
        {
          "playfieldName": "Harbour",
          "playfieldCoordinates": [
            { "latitude": 52.1, "longitude": 4.1 },
            { "latitude": 52.2, "longitude": 4.2 },
            { "latitude": 52.3, "longitude": 4.1 }
          ],
          "hunterUserId": "{{hunterId}}",
          "participants": [
            { "userId": "{{hunterId}}", "callsign": "WOLF", "lastKnownLocation": { "latitude": 52.15, "longitude": 4.15 }, "hasActivePenalty": false, "state": "Active" },
            { "userId": "{{preyId}}", "callsign": "GHOST", "lastKnownLocation": null, "hasActivePenalty": false, "state": "Active" }
          ],
          "gameDurationLeft": 600,
          "nextPingDuration": 30,
          "currentPingInterval": 60,
          "isEndgame": false,
          "preysLeft": 1,
          "hunterMayMoveAt": "2026-07-16T12:00:00Z"
        }
        """;
    }

    [Fact]
    public async Task GetGameStatusDetailsAsync_ShouldReturnRichProjection_WhenBackendReturns200()
    {
        var hunterId = Guid.NewGuid();
        var preyId = Guid.NewGuid();
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, StatusJson(hunterId, preyId));
        var sut = CreateSut(handler);

        var result = await sut.GetGameStatusDetailsAsync(Guid.NewGuid(), "my-token");

        Assert.Equal(GetGameStatusOutcome.Success, result.Outcome);
        Assert.Equal(hunterId, result.Details!.HunterUserId);
        Assert.Equal(3, result.Details.PlayfieldCoordinates.Count);
        Assert.Equal(2, result.Details.Participants.Count);
        var hunter = result.Details.Participants.Single(p => p.UserId == hunterId);
        Assert.NotNull(hunter.LastKnownLocation);
        Assert.Equal(52.15, hunter.LastKnownLocation!.Latitude, 5);
        Assert.Null(result.Details.Participants.Single(p => p.UserId == preyId).LastKnownLocation);
        Assert.Equal(new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero), result.Details.HunterMayMoveAt);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.EndsWith("/status", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, GetGameStatusOutcome.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, GetGameStatusOutcome.Conflict)]
    [InlineData(HttpStatusCode.NotFound, GetGameStatusOutcome.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, GetGameStatusOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, GetGameStatusOutcome.Error)]
    public async Task GetGameStatusDetailsAsync_ShouldMapStatus(HttpStatusCode status, GetGameStatusOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.GetGameStatusDetailsAsync(Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task GetGameStatusDetailsAsync_ShouldReturnError_WhenRequestThrows()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.GetGameStatusDetailsAsync(Guid.NewGuid(), "access");

        Assert.Equal(GetGameStatusOutcome.Error, result.Outcome);
    }
}
