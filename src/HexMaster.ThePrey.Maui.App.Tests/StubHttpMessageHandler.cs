using System.Net;

namespace HexMaster.ThePrey.Maui.App.Tests;

/// <summary>Test double for <see cref="HttpMessageHandler"/> with a scripted response or exception.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>The body of the last request, captured before the client disposes it.</summary>
    public string? LastRequestBody { get; private set; }

    private StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public static StubHttpMessageHandler Returns(HttpStatusCode status, string? json = null) =>
        new(_ =>
        {
            var response = new HttpResponseMessage(status);
            if (json is not null)
                response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            return response;
        });

    public static StubHttpMessageHandler Throws(Exception exception) =>
        new(_ => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

        return _responder(request);
    }
}
