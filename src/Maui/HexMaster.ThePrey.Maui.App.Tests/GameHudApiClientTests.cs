using System.Net;
using HexMaster.ThePrey.Maui.App.Services.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Maui.App.Tests;

/// <summary>Covers the in-game HUD additions to <see cref="GameApiClient"/> (status / state / tag).</summary>
public class GameHudApiClientTests
{
    private static GameApiClient CreateSut(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.example.com/") };
        return new GameApiClient(http, NullLogger<GameApiClient>.Instance);
    }

    // ---- GetGameStatusAsync (9.1) ----

    [Fact]
    public async Task GetGameStatusAsync_ShouldReturnSuccess_WhenBackendReturns200()
    {
        var hunter = Guid.NewGuid();
        var prey = Guid.NewGuid();
        var json = $$"""
        {
          "gameDurationLeft": 900,
          "nextPingDuration": 42,
          "currentPingInterval": 120,
          "isEndgame": false,
          "preysLeft": 1,
          "hunterUserId": "{{hunter}}",
          "participants": [ { "userId": "{{hunter}}" }, { "userId": "{{prey}}" } ]
        }
        """;
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.GetGameStatusAsync(Guid.NewGuid(), "access");

        Assert.Equal(GameStatusOutcome.Success, result.Outcome);
        Assert.NotNull(result.Status);
        Assert.Equal(900, result.Status!.GameDurationLeft);
        Assert.Equal(42, result.Status.NextPingDuration);
        Assert.Equal(120, result.Status.CurrentPingInterval);
        Assert.Equal(1, result.Status.PreysLeft);
        Assert.Equal(hunter, result.Status.HunterUserId);
        Assert.Equal(2, result.Status.Participants.Count);
    }

    [Fact]
    public async Task GetGameStatusAsync_ShouldSendBearerToken_AndCorrectRoute()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.NotFound);
        var sut = CreateSut(handler);
        var gameId = Guid.NewGuid();

        await sut.GetGameStatusAsync(gameId, "my-token");

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal($"/games/{gameId}/status", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, GameStatusOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, GameStatusOutcome.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, GameStatusOutcome.Completed)]
    [InlineData(HttpStatusCode.Unauthorized, GameStatusOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, GameStatusOutcome.Error)]
    public async Task GetGameStatusAsync_ShouldMapStatusCodes(HttpStatusCode status, GameStatusOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.GetGameStatusAsync(Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task GetGameStatusAsync_ShouldReturnError_WhenRequestThrows()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.GetGameStatusAsync(Guid.NewGuid(), "access");

        Assert.Equal(GameStatusOutcome.Error, result.Outcome);
    }

    // ---- GetGameStateAsync (9.1) ----

    [Fact]
    public async Task GetGameStateAsync_ShouldReturnSuccess_WithPreyDistance()
    {
        var json = """{ "hunterDistanceMeters": 250, "preyLocations": [] }""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.GetGameStateAsync(Guid.NewGuid(), "access");

        Assert.Equal(GameStateOutcome.Success, result.Outcome);
        Assert.Equal(250, result.State!.HunterDistanceMeters);
        Assert.Empty(result.State.PreyLocations);
    }

    [Fact]
    public async Task GetGameStateAsync_ShouldReturnSuccess_WithHunterPreyLocations()
    {
        var json = """{ "hunterDistanceMeters": null, "preyLocations": [ { "latitude": 52.1, "longitude": 4.5 } ] }""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.GetGameStateAsync(Guid.NewGuid(), "access");

        Assert.Equal(GameStateOutcome.Success, result.Outcome);
        Assert.Null(result.State!.HunterDistanceMeters);
        Assert.Single(result.State.PreyLocations);
        Assert.Equal(52.1, result.State.PreyLocations[0].Latitude, 6);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, GameStateOutcome.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, GameStateOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, GameStateOutcome.Error)]
    public async Task GetGameStateAsync_ShouldMapStatusCodes(HttpStatusCode status, GameStateOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.GetGameStateAsync(Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }

    // ---- GetTagCandidatesAsync (9.1) ----

    [Fact]
    public async Task GetTagCandidatesAsync_ShouldReturnSuccess_WithCandidatesAndRange()
    {
        var prey = Guid.NewGuid();
        var json = $$"""
        {
          "rangeMeters": 30.0,
          "candidates": [ { "userId": "{{prey}}", "callsign": "GHOST", "state": "Active", "distanceMeters": 12.5 } ]
        }
        """;
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, json);
        var sut = CreateSut(handler);
        var gameId = Guid.NewGuid();

        var result = await sut.GetTagCandidatesAsync(gameId, "access");

        Assert.Equal(TagCandidatesOutcome.Success, result.Outcome);
        Assert.Equal(30.0, result.RangeMeters, 3);
        Assert.Single(result.Candidates);
        Assert.Equal(prey, result.Candidates[0].UserId);
        Assert.Equal("GHOST", result.Candidates[0].Callsign);
        Assert.Equal(12.5, result.Candidates[0].DistanceMeters, 3);
        Assert.Equal($"/games/{gameId}/tag-candidates", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetTagCandidatesAsync_ShouldReturnSuccessEmpty_WhenNoCandidates()
    {
        var json = """{ "rangeMeters": 30.0, "candidates": [] }""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.GetTagCandidatesAsync(Guid.NewGuid(), "access");

        Assert.Equal(TagCandidatesOutcome.Success, result.Outcome);
        Assert.Empty(result.Candidates);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, TagCandidatesOutcome.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, TagCandidatesOutcome.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, TagCandidatesOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, TagCandidatesOutcome.Error)]
    public async Task GetTagCandidatesAsync_ShouldMapStatusCodes(HttpStatusCode status, TagCandidatesOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.GetTagCandidatesAsync(Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }

    // ---- TagPlayerAsync (9.1) ----

    [Fact]
    public async Task TagPlayerAsync_ShouldReturnSuccess_WhenBackendReturns204_AndTargetTheParticipantRoute()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.NoContent);
        var sut = CreateSut(handler);
        var gameId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var result = await sut.TagPlayerAsync(gameId, participantId, "my-token");

        Assert.Equal(TagPlayerOutcome.Success, result.Outcome);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal($"/games/{gameId}/participants/{participantId}/tag", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, TagPlayerOutcome.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, TagPlayerOutcome.NotFound)]
    [InlineData(HttpStatusCode.Conflict, TagPlayerOutcome.Conflict)]
    [InlineData(HttpStatusCode.Unauthorized, TagPlayerOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError, TagPlayerOutcome.Error)]
    public async Task TagPlayerAsync_ShouldMapStatusCodes(HttpStatusCode status, TagPlayerOutcome expected)
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(status));

        var result = await sut.TagPlayerAsync(Guid.NewGuid(), Guid.NewGuid(), "access");

        Assert.Equal(expected, result.Outcome);
    }
}
