using HexMaster.ThePrey.Games.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.GameEngine.Infrastructure;

/// <summary>
/// Outgoing <see cref="DelegatingHandler"/> that adds HMAC-SHA256 signing headers
/// (<c>X-Engine-Timestamp</c> and <c>X-Engine-Signature</c>) to every request with a body.
/// Fails fast with an <see cref="InvalidOperationException"/> when the engine key is not configured.
/// </summary>
internal sealed class EngineRequestSigningHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EngineRequestSigningHandler> _logger;

    public EngineRequestSigningHandler(
        IConfiguration configuration,
        ILogger<EngineRequestSigningHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var secret = _configuration["GameEngine:EngineKey"];
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogError(
                "GameEngine:EngineKey is not configured. Cannot sign outgoing request to {Uri}",
                request.RequestUri);
            throw new InvalidOperationException(
                "GameEngine:EngineKey is not configured. The engine key must be set before the GameEngine can call the Games API.");
        }

        if (request.Content is not null)
        {
            // Buffer the body bytes so we can sign them and still deliver them downstream.
            var bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);

            // Capture content headers before replacing the content object.
            var contentHeaders = request.Content.Headers
                .Select(h => (h.Key, h.Value))
                .ToList();

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = EngineRequestSigner.ComputeSignature(secret, timestamp, bodyBytes);

            request.Headers.Remove(EngineRequestSigner.TimestampHeaderName);
            request.Headers.Remove(EngineRequestSigner.SignatureHeaderName);
            request.Headers.Add(EngineRequestSigner.TimestampHeaderName, timestamp);
            request.Headers.Add(EngineRequestSigner.SignatureHeaderName, signature);

            // Replace the content with the buffered bytes (the original stream may be consumed).
            var bufferedContent = new ByteArrayContent(bodyBytes);
            foreach (var (key, values) in contentHeaders)
                bufferedContent.Headers.TryAddWithoutValidation(key, values);

            request.Content = bufferedContent;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
