using System.Security.Cryptography;
using System.Text;

namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>
/// A PKCE (RFC 7636) verifier/challenge pair for the Authorization Code flow.
/// The verifier is a high-entropy random string; the challenge is its SHA-256 hash (S256).
/// </summary>
public sealed record PkcePair(string Verifier, string Challenge)
{
    public const string ChallengeMethod = "S256";

    public static PkcePair Create()
    {
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64UrlEncode(challengeBytes);
        return new PkcePair(verifier, challenge);
    }

    /// <summary>Cryptographically-random opaque value for the OAuth <c>state</c> parameter.</summary>
    public static string CreateState() => Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
