using HexMaster.ThePrey.Maui.App.Configuration;
using HexMaster.ThePrey.Maui.App.Services.Authentication;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class Auth0LogoutUrlTests
{
    private static readonly ThePreyClientOptions Options = new()
    {
        Auth0Domain = "https://theprey.eu.auth0.com/",
        Auth0ClientId = "client-id",
        RedirectUri = "theprey://callback"
    };

    [Fact]
    public void Build_ShouldTargetTheAuth0LogoutEndpoint()
    {
        var url = Auth0LogoutUrl.Build(Options);

        Assert.Equal("https://theprey.eu.auth0.com/v2/logout", url.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public void Build_ShouldCarryClientIdAndReturnTo()
    {
        var url = Auth0LogoutUrl.Build(Options);

        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        Assert.Equal("client-id", query["client_id"]);
        Assert.Equal("theprey://callback", query["returnTo"]);
    }
}
