using System.Net;
using HexMaster.ThePrey.Maui.App.Services.Location;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class LocationReportClientTests
{
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly RecordLocationRequest _request =
        new(52.1, 4.3, new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero), 5.0);

    private static LocationReportClient CreateSut(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.example.com/") };
        return new LocationReportClient(http, NullLogger<LocationReportClient>.Instance);
    }

    [Fact]
    public async Task ReportAsync_ShouldReturnAcceptedWithInterval_When200()
    {
        var json = """{"accepted":true,"nextLocationIntervalSeconds":15,"penaltyIntervalSeconds":5,"penaltyEndsAt":"2026-07-16T10:05:00+00:00"}""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.ReportAsync(_gameId, _request, "token");

        Assert.Equal(LocationReportOutcome.Accepted, result.Outcome);
        Assert.NotNull(result.Response);
        Assert.True(result.Response!.Accepted);
        Assert.Equal(15, result.Response.NextLocationIntervalSeconds);
        Assert.Equal(5, result.Response.PenaltyIntervalSeconds);
    }

    [Fact]
    public async Task ReportAsync_ShouldPostToLocationsEndpointWithBearerToken()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, """{"accepted":true,"nextLocationIntervalSeconds":10}""");
        var sut = CreateSut(handler);

        await sut.ReportAsync(_gameId, _request, "my-token");

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal($"games/{_gameId}/locations", handler.LastRequest?.RequestUri?.AbsolutePath.TrimStart('/'));
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ReportAsync_ShouldReturnGameOver_When404()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.NotFound));

        var result = await sut.ReportAsync(_gameId, _request, "token");

        Assert.Equal(LocationReportOutcome.GameOver, result.Outcome);
    }

    [Fact]
    public async Task ReportAsync_ShouldReturnGameOver_When422()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns((HttpStatusCode)422));

        var result = await sut.ReportAsync(_gameId, _request, "token");

        Assert.Equal(LocationReportOutcome.GameOver, result.Outcome);
    }

    [Fact]
    public async Task ReportAsync_ShouldReturnUnauthorized_When401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.ReportAsync(_gameId, _request, "token");

        Assert.Equal(LocationReportOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task ReportAsync_ShouldReturnTransient_When500()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.InternalServerError));

        var result = await sut.ReportAsync(_gameId, _request, "token");

        Assert.Equal(LocationReportOutcome.Transient, result.Outcome);
    }

    [Fact]
    public async Task ReportAsync_ShouldReturnTransient_WhenNetworkFails()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.ReportAsync(_gameId, _request, "token");

        Assert.Equal(LocationReportOutcome.Transient, result.Outcome);
    }
}
