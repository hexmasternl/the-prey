using System.Net;
using HexMaster.ThePrey.Maui.App.Configuration;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class Auth0TokenClientTests
{
    private static Auth0TokenClient CreateSut(StubHttpMessageHandler handler)
    {
        var options = Options.Create(new ThePreyClientOptions
        {
            Auth0Domain = "https://theprey.eu.auth0.com/",
            Auth0ClientId = "client-id",
            Audience = "https://api.theprey.nl"
        });
        var http = new HttpClient(handler);
        return new Auth0TokenClient(http, options, NullLogger<Auth0TokenClient>.Instance);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnSuccessWithTokens_WhenAuth0Returns200()
    {
        const string json = """{"access_token":"the-access-token","refresh_token":"rotated-rt","expires_in":86400,"token_type":"Bearer"}""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.RefreshAsync("old-rt");

        Assert.Equal(Auth0TokenOutcome.Success, result.Outcome);
        Assert.Equal("the-access-token", result.AccessToken);
        Assert.Equal("rotated-rt", result.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnRejected_WhenAuth0ReturnsInvalidGrant()
    {
        const string json = """{"error":"invalid_grant","error_description":"Unknown or invalid refresh token."}""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.BadRequest, json));

        var result = await sut.RefreshAsync("dead-rt");

        Assert.Equal(Auth0TokenOutcome.Rejected, result.Outcome);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnTransient_WhenAuth0Returns500()
    {
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.InternalServerError));

        var result = await sut.RefreshAsync("rt");

        Assert.Equal(Auth0TokenOutcome.TransientFailure, result.Outcome);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnTransient_WhenRequestThrows()
    {
        var sut = CreateSut(StubHttpMessageHandler.Throws(new HttpRequestException("no network")));

        var result = await sut.RefreshAsync("rt");

        Assert.Equal(Auth0TokenOutcome.TransientFailure, result.Outcome);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ShouldReturnSuccess_WhenAuth0Returns200()
    {
        const string json = """{"access_token":"acc","refresh_token":"rt","expires_in":86400,"token_type":"Bearer"}""";
        var sut = CreateSut(StubHttpMessageHandler.Returns(HttpStatusCode.OK, json));

        var result = await sut.ExchangeCodeAsync("auth-code", "verifier");

        Assert.Equal(Auth0TokenOutcome.Success, result.Outcome);
        Assert.Equal("acc", result.AccessToken);
        Assert.Equal("rt", result.RefreshToken);
    }
}
