using System.Net;
using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class UserApiClientTests
{
    private static UserApiClient CreateSut(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://gateway.example.com/")
        };
        return new UserApiClient(http, NullLogger<UserApiClient>.Instance);
    }

    private static string UserJson(string displayName = "Ghost", string language = "en") =>
        $$"""{"userId":"{{Guid.NewGuid()}}","displayName":"{{displayName}}","callsign":"Reaper","emailAddress":"a@b.c","preferredLanguage":"{{language}}"}""";

    // ---- GET /users/me ----

    [Fact]
    public async Task GetCurrentUserAsync_ShouldReturnSuccess_WhenBackendReturns200()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, UserJson("Ghost", "nl")));

        var result = await sut.GetCurrentUserAsync("access");

        Assert.Equal(UserSettingsOutcome.Success, result.Outcome);
        Assert.NotNull(result.Settings);
        Assert.Equal("Ghost", result.Settings!.DisplayName);
        Assert.Equal("nl", result.Settings.PreferredLanguage);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldSendBearerToken()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, UserJson());
        var sut = CreateSut(handler);

        await sut.GetCurrentUserAsync("my-access-token");

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-access-token", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldReturnUnauthorized_WhenBackendReturns401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.GetCurrentUserAsync("access");

        Assert.Equal(UserSettingsOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldReturnNotFound_WhenBackendReturns404()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.NotFound));

        var result = await sut.GetCurrentUserAsync("access");

        Assert.Equal(UserSettingsOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldReturnError_WhenNetworkFails()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.GetCurrentUserAsync("access");

        Assert.Equal(UserSettingsOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldReturnError_WhenRequestTimesOut()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new TaskCanceledException("timeout")));

        var result = await sut.GetCurrentUserAsync("access");

        Assert.Equal(UserSettingsOutcome.Error, result.Outcome);
    }

    // ---- PUT /users/me ----

    [Fact]
    public async Task UpdateUserAsync_ShouldReturnSuccess_WhenBackendReturns200()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, UserJson("Wraith", "nl")));

        var result = await sut.UpdateUserAsync(new UserSettings("Wraith", "nl"), "access");

        Assert.Equal(SaveSettingsOutcome.Success, result.Outcome);
        Assert.Equal("Wraith", result.Settings!.DisplayName);
        Assert.Equal("nl", result.Settings.PreferredLanguage);
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldSendBearerTokenAndBody()
    {
        var handler = StubHttpMessageHandler.Returns(HttpStatusCode.OK, UserJson());
        var sut = CreateSut(handler);

        await sut.UpdateUserAsync(new UserSettings("Ghost", "nl"), "my-token");

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("my-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);

        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("Ghost", doc.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("nl", doc.RootElement.GetProperty("preferredLanguage").GetString());
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldReturnValidationFailed_WhenBackendReturns400()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.BadRequest));

        var result = await sut.UpdateUserAsync(new UserSettings("", "en"), "access");

        Assert.Equal(SaveSettingsOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldReturnUnauthorized_WhenBackendReturns401()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await sut.UpdateUserAsync(new UserSettings("Ghost", "en"), "access");

        Assert.Equal(SaveSettingsOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldReturnNotFound_WhenBackendReturns404()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.NotFound));

        var result = await sut.UpdateUserAsync(new UserSettings("Ghost", "en"), "access");

        Assert.Equal(SaveSettingsOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldReturnError_WhenNetworkFails()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await sut.UpdateUserAsync(new UserSettings("Ghost", "en"), "access");

        Assert.Equal(SaveSettingsOutcome.Error, result.Outcome);
    }
}
