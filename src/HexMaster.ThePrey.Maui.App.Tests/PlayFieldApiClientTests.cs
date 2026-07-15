using System.Net;
using HexMaster.ThePrey.Maui.App.Services.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class PlayFieldApiClientTests
{
    private static PlayFieldApiClient CreateSut(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://gateway.example.com/")
        };
        return new PlayFieldApiClient(http, NullLogger<PlayFieldApiClient>.Instance);
    }

    private static string SummaryJson(string name, bool isPublic) =>
        $$"""{"id":"{{Guid.NewGuid()}}","name":"{{name}}","ownerId":"{{Guid.NewGuid()}}","isPublic":{{(isPublic ? "true" : "false")}},"lastUpdatedOn":"2026-07-15T00:00:00+00:00","centerCoordinates":null}""";

    // ---- GET /playfields ----

    [Fact]
    public async Task GetMyPlayFieldsAsync_ShouldReturnSuccessWithItems_WhenBackendReturns200()
    {
        var json = $"[{SummaryJson("Alpha", true)},{SummaryJson("Bravo", false)}]";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.GetMyPlayFieldsAsync("access");

        Assert.Equal(MyPlayFieldsOutcome.Success, result.Outcome);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Alpha", result.Items[0].Name);
        Assert.True(result.Items[0].IsPublic);
        Assert.False(result.Items[1].IsPublic);
    }

    [Fact]
    public async Task GetMyPlayFieldsAsync_ShouldReturnEmptySuccess_WhenBackendReturnsEmptyList()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, "[]"));

        var result = await sut.GetMyPlayFieldsAsync("access");

        Assert.Equal(MyPlayFieldsOutcome.Success, result.Outcome);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetMyPlayFieldsAsync_ShouldSendBearerToken()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, "[]");
        var sut = CreateSut(handler);

        await sut.GetMyPlayFieldsAsync("my-access-token");

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-access-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("/playfields", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetMyPlayFieldsAsync_ShouldReturnUnauthorized_WhenBackendReturns401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.GetMyPlayFieldsAsync("access");

        Assert.Equal(MyPlayFieldsOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task GetMyPlayFieldsAsync_ShouldReturnError_WhenNetworkFails()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.GetMyPlayFieldsAsync("access");

        Assert.Equal(MyPlayFieldsOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task GetMyPlayFieldsAsync_ShouldReturnError_WhenRequestTimesOut()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new TaskCanceledException("timeout")));

        var result = await sut.GetMyPlayFieldsAsync("access");

        Assert.Equal(MyPlayFieldsOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task GetMyPlayFieldsAsync_ShouldReturnError_WhenBackendReturnsUnexpectedStatus()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.InternalServerError));

        var result = await sut.GetMyPlayFieldsAsync("access");

        Assert.Equal(MyPlayFieldsOutcome.Error, result.Outcome);
    }

    // ---- GET /playfields/public ----

    [Fact]
    public async Task SearchPublicPlayFieldsAsync_ShouldReturnSuccess_WhenBackendReturns200()
    {
        var json = $"[{SummaryJson("Downtown", true)}]";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.SearchPublicPlayFieldsAsync("dow", "access");

        Assert.Equal(PublicPlayFieldsOutcome.Success, result.Outcome);
        Assert.Single(result.Items);
        Assert.Equal("Downtown", result.Items[0].Name);
    }

    [Fact]
    public async Task SearchPublicPlayFieldsAsync_ShouldReturnEmptySuccess_WhenBackendReturnsEmptyList()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, "[]"));

        var result = await sut.SearchPublicPlayFieldsAsync("zzz", "access");

        Assert.Equal(PublicPlayFieldsOutcome.Success, result.Outcome);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchPublicPlayFieldsAsync_ShouldSendBearerTokenAndQueryParameter()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, "[]");
        var sut = CreateSut(handler);

        await sut.SearchPublicPlayFieldsAsync("river bank", "my-token");

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("/playfields/public", handler.LastRequest?.RequestUri?.AbsolutePath);
        var query = handler.LastRequest?.RequestUri?.Query;
        Assert.Contains("q=river", query);
        Assert.DoesNotContain(" ", query); // the query value is URL-encoded
    }

    [Fact]
    public async Task SearchPublicPlayFieldsAsync_ShouldReturnValidationTooShort_WhenBackendReturns400()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.BadRequest));

        var result = await sut.SearchPublicPlayFieldsAsync("ab", "access");

        Assert.Equal(PublicPlayFieldsOutcome.ValidationTooShort, result.Outcome);
    }

    [Fact]
    public async Task SearchPublicPlayFieldsAsync_ShouldReturnUnauthorized_WhenBackendReturns401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.SearchPublicPlayFieldsAsync("abc", "access");

        Assert.Equal(PublicPlayFieldsOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task SearchPublicPlayFieldsAsync_ShouldReturnError_WhenNetworkFails()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.SearchPublicPlayFieldsAsync("abc", "access");

        Assert.Equal(PublicPlayFieldsOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task SearchPublicPlayFieldsAsync_ShouldReturnError_WhenRequestTimesOut()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new TaskCanceledException("timeout")));

        var result = await sut.SearchPublicPlayFieldsAsync("abc", "access");

        Assert.Equal(PublicPlayFieldsOutcome.Error, result.Outcome);
    }
}
