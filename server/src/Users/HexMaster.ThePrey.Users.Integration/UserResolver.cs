using System.Diagnostics;
using System.Net;
using Dapr.Client;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace HexMaster.ThePrey.Users.Integration;

public sealed class UserResolver : IUserResolver
{
    private static readonly ActivitySource ActivitySource = new("HexMaster.ThePrey.Users");

    private readonly DaprClient _dapr;
    private readonly UserResolverOptions _options;
    private readonly ILogger<UserResolver> _logger;

    public UserResolver(
        DaprClient dapr,
        IOptions<UserResolverOptions> options,
        ILogger<UserResolver> logger)
    {
        _dapr = dapr;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UserDto?> ResolveUser(string subjectId, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("ResolveUser");

        try
        {
            var cacheKey = $"user-subject:{subjectId}";

            var cached = await _dapr.GetStateAsync<UserDto>(_options.StateStoreName, cacheKey, cancellationToken: ct);
            if (cached is not null)
            {
                activity?.SetTag("user.cache_hit", true);
                return cached;
            }

            activity?.SetTag("user.cache_hit", false);

            UserDto? user;
            try
            {
                var request = _dapr.CreateInvokeMethodRequest(HttpMethod.Get, _options.UsersAppId, $"internal/users/{subjectId}");
                // Dapr 1.17 marks the HttpRequestMessage overload [Obsolete] in favour of native HTTP/gRPC
                // clients. We keep it deliberately: it is the only abstract (Moq-able) invocation overload,
                // and migrating off Dapr service invocation is an architectural change tracked separately.
#pragma warning disable CS0618
                user = await _dapr.InvokeMethodAsync<UserDto>(request, ct);
#pragma warning restore CS0618
            }
            catch (InvocationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("User with subject {SubjectId} not found via service invocation", subjectId);
                return null;
            }

            if (user is not null)
            {
                var metadata = new Dictionary<string, string>
                {
                    ["ttlInSeconds"] = _options.CacheTtlSeconds.ToString()
                };

                await _dapr.SaveStateAsync(_options.StateStoreName, cacheKey, user, metadata: metadata, cancellationToken: ct);
            }

            return user;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
