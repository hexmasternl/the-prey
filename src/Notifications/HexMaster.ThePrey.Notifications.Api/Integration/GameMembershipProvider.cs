using System.Net;
using Dapr.Client;
using HexMaster.ThePrey.Notifications;
using ThePrey.Aspire.ServiceDefaults;

namespace HexMaster.ThePrey.Notifications.Api.Integration;

/// <summary>
/// Resolves game membership by invoking the Games module over Dapr service invocation. A 2xx response
/// means the user is the owner or a participant; a 404 means they are not a member.
/// </summary>
public sealed class GameMembershipProvider : IGameMembershipProvider
{
    private static readonly string GamesAppId = AspireConstants.Resources.GamesApi;

    private readonly DaprClient _dapr;

    public GameMembershipProvider(DaprClient dapr) => _dapr = dapr;

    public async Task<bool> IsMemberAsync(Guid gameId, Guid userId, CancellationToken ct)
    {
        try
        {
            var request = _dapr.CreateInvokeMethodRequest(
                HttpMethod.Get,
                GamesAppId,
                $"internal/games/{gameId}/members/{userId}");

            // See PlayfieldInfoProvider: this HttpRequestMessage overload is the only mockable one.
#pragma warning disable CS0618
            await _dapr.InvokeMethodAsync(request, ct);
#pragma warning restore CS0618
            return true;
        }
        catch (InvocationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
