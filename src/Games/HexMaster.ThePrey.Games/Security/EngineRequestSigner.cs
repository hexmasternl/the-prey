using System.Security.Cryptography;
using System.Text;

namespace HexMaster.ThePrey.Games.Security;

/// <summary>
/// Shared HMAC-SHA256 signing helper for the GameEngine → Games API location-update endpoint.
/// Both the signing side (GameEngine) and the verification side (Games.Api) reference this type
/// through their common dependency on HexMaster.ThePrey.Games.
/// </summary>
public static class EngineRequestSigner
{
    public const string TimestampHeaderName = "X-Engine-Timestamp";
    public const string SignatureHeaderName = "X-Engine-Signature";

    /// <summary>Default tolerance window for timestamp freshness checks (± 5 minutes).</summary>
    public static readonly TimeSpan DefaultToleranceWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Computes an HMAC-SHA256 signature over <c>{timestamp}.{body}</c>.
    /// </summary>
    /// <param name="secret">Shared secret (UTF-8 encoded as the HMAC key).</param>
    /// <param name="timestamp">Unix seconds string included in the signed payload.</param>
    /// <param name="body">Raw request body bytes.</param>
    /// <returns>Lowercase hex-encoded HMAC-SHA256 digest.</returns>
    public static string ComputeSignature(string secret, string timestamp, ReadOnlySpan<byte> body)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.");

        // Allocate combined payload: "{timestamp}.{body}"
        var payload = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload.AsSpan(prefix.Length));

        var hash = HMACSHA256.HashData(keyBytes, payload);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Verifies the timestamp freshness and HMAC-SHA256 signature of an incoming request.
    /// </summary>
    /// <param name="secret">Shared secret.</param>
    /// <param name="timestampHeader">Value of the <c>X-Engine-Timestamp</c> header.</param>
    /// <param name="signatureHeader">Value of the <c>X-Engine-Signature</c> header (lowercase hex).</param>
    /// <param name="body">Raw request body bytes.</param>
    /// <param name="now">Current time (injected for testability).</param>
    /// <param name="toleranceWindow">How far in the past or future the timestamp may be.</param>
    /// <returns><c>true</c> if the request is both fresh and correctly signed.</returns>
    public static bool Verify(
        string secret,
        string? timestampHeader,
        string? signatureHeader,
        ReadOnlySpan<byte> body,
        DateTimeOffset now,
        TimeSpan? toleranceWindow = null)
    {
        if (string.IsNullOrEmpty(timestampHeader) || string.IsNullOrEmpty(signatureHeader))
            return false;

        if (!long.TryParse(timestampHeader, out var unixSeconds))
            return false;

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var window = toleranceWindow ?? DefaultToleranceWindow;
        var age = now - requestTime;

        if (age < -window || age > window)
            return false;

        var expected = ComputeSignature(secret, timestampHeader, body);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(signatureHeader);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
