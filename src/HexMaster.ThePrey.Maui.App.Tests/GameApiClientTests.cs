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
}
