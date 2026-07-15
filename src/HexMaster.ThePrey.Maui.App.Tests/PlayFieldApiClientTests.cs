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

    // ---- POST /playfields ----

    private static readonly IReadOnlyList<GpsCoordinate> Triangle =
    [
        new(52.1, 4.3),
        new(52.2, 4.4),
        new(52.15, 4.5)
    ];

    [Fact]
    public async Task CreatePlayFieldAsync_ShouldReturnSuccessWithSummary_WhenBackendReturns201()
    {
        var id = Guid.NewGuid();
        var json = $$"""{"id":"{{id}}","name":"NL, Amsterdam, City park","ownerId":"{{Guid.NewGuid()}}","isPublic":true,"points":[],"lastUpdatedOn":"2026-07-15T00:00:00+00:00","centerCoordinates":null}""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Created, json));

        var result = await sut.CreatePlayFieldAsync("NL, Amsterdam, City park", true, Triangle, "access");

        Assert.Equal(CreatePlayFieldOutcome.Success, result.Outcome);
        Assert.NotNull(result.PlayField);
        Assert.Equal(id, result.PlayField!.Id);
        Assert.Equal("NL, Amsterdam, City park", result.PlayField.Name);
        Assert.True(result.PlayField.IsPublic);
    }

    [Fact]
    public async Task CreatePlayFieldAsync_ShouldPostBearerTokenAndRequestBody()
    {
        var handler = StubHttpMessageHandler.Returns(
            HttpStatusCode.Created,
            $$"""{"id":"{{Guid.NewGuid()}}","name":"NL, Amsterdam, City park","ownerId":"{{Guid.NewGuid()}}","isPublic":false,"points":[],"lastUpdatedOn":"2026-07-15T00:00:00+00:00","centerCoordinates":null}""");
        var sut = CreateSut(handler);

        await sut.CreatePlayFieldAsync("NL, Amsterdam, City park", false, Triangle, "my-token");

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/playfields", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);

        var body = handler.LastRequestBody;
        Assert.NotNull(body);
        Assert.Contains("\"name\":\"NL, Amsterdam, City park\"", body);
        Assert.Contains("\"isPublic\":false", body);
        Assert.Contains("\"latitude\":52.1", body);
        Assert.Contains("\"longitude\":4.3", body);
    }

    [Fact]
    public async Task CreatePlayFieldAsync_ShouldReturnValidation_WhenBackendReturns400()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.BadRequest));

        var result = await sut.CreatePlayFieldAsync("NL, Amsterdam, City park", true, Triangle, "access");

        Assert.Equal(CreatePlayFieldOutcome.Validation, result.Outcome);
        Assert.Null(result.PlayField);
    }

    [Fact]
    public async Task CreatePlayFieldAsync_ShouldReturnUnauthorized_WhenBackendReturns401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.CreatePlayFieldAsync("NL, Amsterdam, City park", true, Triangle, "access");

        Assert.Equal(CreatePlayFieldOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task CreatePlayFieldAsync_ShouldReturnError_WhenNetworkFails()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.CreatePlayFieldAsync("NL, Amsterdam, City park", true, Triangle, "access");

        Assert.Equal(CreatePlayFieldOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task CreatePlayFieldAsync_ShouldReturnError_WhenRequestTimesOut()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new TaskCanceledException("timeout")));

        var result = await sut.CreatePlayFieldAsync("NL, Amsterdam, City park", true, Triangle, "access");

        Assert.Equal(CreatePlayFieldOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task CreatePlayFieldAsync_ShouldReturnError_WhenBackendReturnsUnexpectedStatus()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.InternalServerError));

        var result = await sut.CreatePlayFieldAsync("NL, Amsterdam, City park", true, Triangle, "access");

        Assert.Equal(CreatePlayFieldOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task CreatePlayFieldAsync_ShouldReturnError_WhenCreatedPayloadCannotBeParsed()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Created, "not json"));

        var result = await sut.CreatePlayFieldAsync("NL, Amsterdam, City park", true, Triangle, "access");

        Assert.Equal(CreatePlayFieldOutcome.Error, result.Outcome);
    }

    // ---- DELETE /playfields/{id} ----

    [Fact]
    public async Task DeletePlayFieldAsync_ShouldReturnSuccess_WhenBackendReturns204()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.NoContent));

        var result = await sut.DeletePlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(DeletePlayFieldOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task DeletePlayFieldAsync_ShouldSendDeleteWithBearerTokenToTheIdRoute()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.NoContent);
        var sut = CreateSut(handler);
        var id = Guid.NewGuid();

        await sut.DeletePlayFieldAsync(id, "my-token");

        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/playfields/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task DeletePlayFieldAsync_ShouldReturnNotFound_WhenBackendReturns404()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.NotFound));

        var result = await sut.DeletePlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(DeletePlayFieldOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task DeletePlayFieldAsync_ShouldReturnForbidden_WhenBackendReturns403()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Forbidden));

        var result = await sut.DeletePlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(DeletePlayFieldOutcome.Forbidden, result.Outcome);
    }

    [Fact]
    public async Task DeletePlayFieldAsync_ShouldReturnUnauthorized_WhenBackendReturns401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.DeletePlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(DeletePlayFieldOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task DeletePlayFieldAsync_ShouldReturnError_WhenNetworkFails()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.DeletePlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(DeletePlayFieldOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task DeletePlayFieldAsync_ShouldReturnError_WhenRequestTimesOut()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new TaskCanceledException("timeout")));

        var result = await sut.DeletePlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(DeletePlayFieldOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task DeletePlayFieldAsync_ShouldReturnError_WhenBackendReturnsUnexpectedStatus()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.InternalServerError));

        var result = await sut.DeletePlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(DeletePlayFieldOutcome.Error, result.Outcome);
    }

    // ---- GET /playfields/{id} ----

    private static string FullJson(Guid id, string name, bool isPublic, string lastUpdatedOn) =>
        $$"""
        {"id":"{{id}}","name":"{{name}}","ownerId":"{{Guid.NewGuid()}}","isPublic":{{(isPublic ? "true" : "false")}},"points":[{"latitude":52.1,"longitude":4.3},{"latitude":52.2,"longitude":4.4},{"latitude":52.15,"longitude":4.5}],"lastUpdatedOn":"{{lastUpdatedOn}}","centerCoordinates":null}
        """;

    [Fact]
    public async Task GetPlayFieldAsync_ShouldReturnSuccessWithDetails_WhenBackendReturns200()
    {
        var id = Guid.NewGuid();
        var sut = CreateSut(StubHttpMessageHandler.Returns(
            HttpStatusCode.OK, FullJson(id, "NL, Amsterdam, City park", true, "2026-07-15T10:00:00+00:00")));

        var result = await sut.GetPlayFieldAsync(id, "access");

        Assert.Equal(GetPlayFieldOutcome.Success, result.Outcome);
        Assert.NotNull(result.PlayField);
        Assert.Equal(id, result.PlayField!.Id);
        Assert.Equal("NL, Amsterdam, City park", result.PlayField.Name);
        Assert.True(result.PlayField.IsPublic);
        Assert.Equal(3, result.PlayField.Points.Count);
        Assert.Equal(52.1, result.PlayField.Points[0].Latitude);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero), result.PlayField.LastUpdatedOn);
    }

    [Fact]
    public async Task GetPlayFieldAsync_ShouldSendBearerTokenToTheIdRoute()
    {
        var id = Guid.NewGuid();
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, FullJson(id, "N, A, B", false, "2026-07-15T10:00:00+00:00"));
        var sut = CreateSut(handler);

        await sut.GetPlayFieldAsync(id, "my-token");

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal($"/playfields/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetPlayFieldAsync_ShouldReturnNotFound_WhenBackendReturns404()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.NotFound));

        var result = await sut.GetPlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(GetPlayFieldOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task GetPlayFieldAsync_ShouldReturnUnauthorized_WhenBackendReturns401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.GetPlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(GetPlayFieldOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task GetPlayFieldAsync_ShouldReturnError_WhenNetworkFails()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.GetPlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(GetPlayFieldOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task GetPlayFieldAsync_ShouldReturnError_WhenRequestTimesOut()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new TaskCanceledException("timeout")));

        var result = await sut.GetPlayFieldAsync(Guid.NewGuid(), "access");

        Assert.Equal(GetPlayFieldOutcome.Error, result.Outcome);
    }

    // ---- PUT /playfields/{id} ----

    private static readonly DateTimeOffset Stamp = new(2026, 7, 15, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task UpdatePlayFieldAsync_ShouldReturnUpdatedSummary_WhenBackendReturns200()
    {
        var id = Guid.NewGuid();
        var json = $$"""{"id":"{{id}}","name":"NL, Amsterdam, City park","ownerId":"{{Guid.NewGuid()}}","isPublic":false,"points":[],"lastUpdatedOn":"2026-07-15T11:00:00+00:00","centerCoordinates":null}""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.UpdatePlayFieldAsync(id, "NL, Amsterdam, City park", false, Triangle, Stamp, "access");

        Assert.Equal(UpdatePlayFieldOutcome.Updated, result.Outcome);
        Assert.NotNull(result.Summary);
        Assert.Equal(id, result.Summary!.Id);
        Assert.Equal("NL, Amsterdam, City park", result.Summary.Name);
    }

    [Fact]
    public async Task UpdatePlayFieldAsync_ShouldPutBearerTokenAndBodyWithLastUpdatedOn()
    {
        var id = Guid.NewGuid();
        var handler = StubHttpMessageHandler.Returns(
            HttpStatusCode.OK,
            $$"""{"id":"{{id}}","name":"N, A, B","ownerId":"{{Guid.NewGuid()}}","isPublic":true,"points":[],"lastUpdatedOn":"2026-07-15T11:00:00+00:00","centerCoordinates":null}""");
        var sut = CreateSut(handler);

        await sut.UpdatePlayFieldAsync(id, "N, A, B", true, Triangle, Stamp, "my-token");

        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);
        Assert.Equal($"/playfields/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        var body = handler.LastRequestBody;
        Assert.NotNull(body);
        Assert.Contains("\"lastUpdatedOn\":", body);
        Assert.Contains("2026-07-15T09:30:00", body);
        Assert.Contains("\"latitude\":52.1", body);
    }

    [Fact]
    public async Task UpdatePlayFieldAsync_ShouldReturnConflictWithCurrent_WhenBackendReturns409()
    {
        var id = Guid.NewGuid();
        var sut = CreateSut(StubHttpMessageHandler.Returns(
            HttpStatusCode.Conflict, FullJson(id, "NL, Amsterdam, Newer name", true, "2026-07-15T12:00:00+00:00")));

        var result = await sut.UpdatePlayFieldAsync(id, "NL, Amsterdam, City park", false, Triangle, Stamp, "access");

        Assert.Equal(UpdatePlayFieldOutcome.Conflict, result.Outcome);
        Assert.NotNull(result.Current);
        Assert.Equal("NL, Amsterdam, Newer name", result.Current!.Name);
        Assert.Equal(3, result.Current.Points.Count);
    }

    [Fact]
    public async Task UpdatePlayFieldAsync_ShouldReturnValidation_WhenBackendReturns400()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.BadRequest));

        var result = await sut.UpdatePlayFieldAsync(Guid.NewGuid(), "N, A, B", true, Triangle, Stamp, "access");

        Assert.Equal(UpdatePlayFieldOutcome.Validation, result.Outcome);
    }

    [Fact]
    public async Task UpdatePlayFieldAsync_ShouldReturnUnauthorized_WhenBackendReturns401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.UpdatePlayFieldAsync(Guid.NewGuid(), "N, A, B", true, Triangle, Stamp, "access");

        Assert.Equal(UpdatePlayFieldOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task UpdatePlayFieldAsync_ShouldReturnForbidden_WhenBackendReturns403()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Forbidden));

        var result = await sut.UpdatePlayFieldAsync(Guid.NewGuid(), "N, A, B", true, Triangle, Stamp, "access");

        Assert.Equal(UpdatePlayFieldOutcome.Forbidden, result.Outcome);
    }

    [Fact]
    public async Task UpdatePlayFieldAsync_ShouldReturnNotFound_WhenBackendReturns404()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.NotFound));

        var result = await sut.UpdatePlayFieldAsync(Guid.NewGuid(), "N, A, B", true, Triangle, Stamp, "access");

        Assert.Equal(UpdatePlayFieldOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdatePlayFieldAsync_ShouldReturnError_WhenNetworkFails()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.UpdatePlayFieldAsync(Guid.NewGuid(), "N, A, B", true, Triangle, Stamp, "access");

        Assert.Equal(UpdatePlayFieldOutcome.Error, result.Outcome);
    }
}
