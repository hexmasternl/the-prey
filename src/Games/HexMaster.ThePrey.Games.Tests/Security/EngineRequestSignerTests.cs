using System.Text;
using HexMaster.ThePrey.Games.Security;

namespace HexMaster.ThePrey.Games.Tests.Security;

public sealed class EngineRequestSignerTests
{
    private const string Secret = "super-secret-engine-key-for-testing";
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    // -----------------------------------------------------------------------
    // ComputeSignature
    // -----------------------------------------------------------------------

    [Fact]
    public void ComputeSignature_ShouldReturnLowercaseHex()
    {
        var sig = EngineRequestSigner.ComputeSignature(Secret, "1234567890", "body"u8);

        Assert.False(string.IsNullOrEmpty(sig));
        Assert.Equal(sig, sig.ToLowerInvariant());
        // HMAC-SHA256 is always 64 hex characters.
        Assert.Equal(64, sig.Length);
    }

    [Fact]
    public void ComputeSignature_ShouldProduceDifferentResults_ForDifferentSecrets()
    {
        var timestamp = "1234567890";
        var body = "hello"u8;

        var sig1 = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);
        var sig2 = EngineRequestSigner.ComputeSignature("other-secret", timestamp, body);

        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public void ComputeSignature_ShouldProduceDifferentResults_ForDifferentTimestamps()
    {
        var body = "hello"u8;

        var sig1 = EngineRequestSigner.ComputeSignature(Secret, "1000000000", body);
        var sig2 = EngineRequestSigner.ComputeSignature(Secret, "1000000001", body);

        Assert.NotEqual(sig1, sig2);
    }

    // -----------------------------------------------------------------------
    // Verify — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Verify_ShouldReturnTrue_WhenSignatureIsValidAndTimestampIsFresh()
    {
        var body = Encoding.UTF8.GetBytes("""{"locations":[]}""");
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);

        var result = EngineRequestSigner.Verify(Secret, timestamp, signature, body, Now);

        Assert.True(result);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenTimestampIsJustWithinPositiveTolerance()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var requestTime = Now - TimeSpan.FromMinutes(4) - TimeSpan.FromSeconds(59);
        var timestamp = requestTime.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);

        var result = EngineRequestSigner.Verify(Secret, timestamp, signature, body, Now);

        Assert.True(result);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenTimestampIsJustWithinNegativeTolerance()
    {
        // Clock skew: request timestamped slightly in the future.
        var body = Encoding.UTF8.GetBytes("{}");
        var requestTime = Now + TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59);
        var timestamp = requestTime.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);

        var result = EngineRequestSigner.Verify(Secret, timestamp, signature, body, Now);

        Assert.True(result);
    }

    // -----------------------------------------------------------------------
    // Verify — tampered body
    // -----------------------------------------------------------------------

    [Fact]
    public void Verify_ShouldReturnFalse_WhenBodyIsTampered()
    {
        var originalBody = Encoding.UTF8.GetBytes("""{"locations":[]}""");
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, originalBody);

        var tamperedBody = Encoding.UTF8.GetBytes("""{"locations":[{"userId":"00000000-0000-0000-0000-000000000001"}]}""");

        var result = EngineRequestSigner.Verify(Secret, timestamp, signature, tamperedBody, Now);

        Assert.False(result);
    }

    // -----------------------------------------------------------------------
    // Verify — wrong secret
    // -----------------------------------------------------------------------

    [Fact]
    public void Verify_ShouldReturnFalse_WhenSecretIsWrong()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);

        var result = EngineRequestSigner.Verify("wrong-secret", timestamp, signature, body, Now);

        Assert.False(result);
    }

    // -----------------------------------------------------------------------
    // Verify — stale / future timestamps
    // -----------------------------------------------------------------------

    [Fact]
    public void Verify_ShouldReturnFalse_WhenTimestampIsOlderThanTolerance()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        // 5 minutes and 1 second older than "now"
        var requestTime = Now - TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1);
        var timestamp = requestTime.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);

        var result = EngineRequestSigner.Verify(Secret, timestamp, signature, body, Now);

        Assert.False(result);
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenTimestampIsFurtherInFutureThanTolerance()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        // 5 minutes and 1 second ahead of "now"
        var requestTime = Now + TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1);
        var timestamp = requestTime.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);

        var result = EngineRequestSigner.Verify(Secret, timestamp, signature, body, Now);

        Assert.False(result);
    }

    // -----------------------------------------------------------------------
    // Verify — malformed / missing headers
    // -----------------------------------------------------------------------

    [Fact]
    public void Verify_ShouldReturnFalse_WhenTimestampHeaderIsNull()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);

        var result = EngineRequestSigner.Verify(Secret, null, signature, body, Now);

        Assert.False(result);
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenSignatureHeaderIsNull()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var timestamp = Now.ToUnixTimeSeconds().ToString();

        var result = EngineRequestSigner.Verify(Secret, timestamp, null, body, Now);

        Assert.False(result);
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenTimestampIsNotANumber()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);

        var result = EngineRequestSigner.Verify(Secret, "not-a-number", signature, body, Now);

        Assert.False(result);
    }

    // -----------------------------------------------------------------------
    // Custom tolerance window
    // -----------------------------------------------------------------------

    [Fact]
    public void Verify_ShouldRespectCustomToleranceWindow()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        // 30 seconds old — within 1-minute window, outside 10-second window.
        var requestTime = Now - TimeSpan.FromSeconds(30);
        var timestamp = requestTime.ToUnixTimeSeconds().ToString();
        var signature = EngineRequestSigner.ComputeSignature(Secret, timestamp, body);

        Assert.True(EngineRequestSigner.Verify(Secret, timestamp, signature, body, Now, TimeSpan.FromMinutes(1)));
        Assert.False(EngineRequestSigner.Verify(Secret, timestamp, signature, body, Now, TimeSpan.FromSeconds(10)));
    }
}
