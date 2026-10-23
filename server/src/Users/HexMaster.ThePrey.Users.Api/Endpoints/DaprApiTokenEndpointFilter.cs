using Microsoft.AspNetCore.Http;

namespace HexMaster.ThePrey.Users.Api.Endpoints;

internal sealed class DaprApiTokenEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expectedToken = Environment.GetEnvironmentVariable("DAPR_APP_API_TOKEN");
        if (!string.IsNullOrEmpty(expectedToken))
        {
            if (!context.HttpContext.Request.Headers.TryGetValue("dapr-api-token", out var received)
                || received != expectedToken)
            {
                return Results.Unauthorized();
            }
        }
        return await next(context);
    }
}
