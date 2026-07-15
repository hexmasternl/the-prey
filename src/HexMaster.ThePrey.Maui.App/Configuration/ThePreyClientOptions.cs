namespace HexMaster.ThePrey.Maui.App.Configuration;

/// <summary>
/// Strongly-typed client configuration for the MAUI app. Bound from the embedded
/// <c>appsettings.json</c> "ThePrey" section. Contains no secrets — the Auth0 native
/// application is a public client that authenticates with Authorization Code + PKCE.
/// </summary>
public sealed class ThePreyClientOptions
{
    public const string SectionName = "ThePrey";

    /// <summary>Auth0 tenant authority, e.g. <c>https://theprey.eu.auth0.com/</c> (trailing slash).</summary>
    public string Auth0Domain { get; set; } = "https://theprey.eu.auth0.com/";

    /// <summary>Client ID of the native/mobile Auth0 application.</summary>
    public string Auth0ClientId { get; set; } = string.Empty;

    /// <summary>API identifier requested as the token audience.</summary>
    public string Audience { get; set; } = "https://api.theprey.nl";

    /// <summary>Custom-scheme callback URL registered as an Allowed Callback URL in Auth0.</summary>
    public string RedirectUri { get; set; } = "theprey://callback";

    /// <summary>Base URL of the backend gateway.</summary>
    public string BackendBaseUrl { get; set; } = "https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io";

    /// <summary>
    /// Base URL for game invite deep links. The lobby appends <c>/{gameCode}</c> to build the join link
    /// it shares (e.g. <c>https://theprey.nl/join/1234</c>). Recognising the link is a separate change.
    /// </summary>
    public string JoinLinkBaseUrl { get; set; } = "https://theprey.nl/join";

    /// <summary>Auth0 OAuth token endpoint derived from <see cref="Auth0Domain"/>.</summary>
    public Uri TokenEndpoint => new(new Uri(NormalizedDomain), "oauth/token");

    /// <summary>Auth0 authorize endpoint derived from <see cref="Auth0Domain"/>.</summary>
    public Uri AuthorizeEndpoint => new(new Uri(NormalizedDomain), "authorize");

    private string NormalizedDomain => Auth0Domain.EndsWith('/') ? Auth0Domain : Auth0Domain + "/";
}
