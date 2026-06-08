using System.Diagnostics;
using System.Net;
using Dapr.Client;
using HexMaster.ThePrey.Games;
using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;
using GamesGpsCoordinateDto = HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GpsCoordinateDto;
using OpenTelemetry.Trace;

namespace HexMaster.ThePrey.Games.Api.Integration;

public sealed class PlayfieldInfoProvider : IPlayfieldInfoProvider
{
    private const string PlayfieldsAppId = "hexmaster-theprey-playfields-api";
    private static readonly ActivitySource ActivitySource = new("HexMaster.ThePrey.Games");

    private readonly DaprClient _dapr;

    public PlayfieldInfoProvider(DaprClient dapr) => _dapr = dapr;

    public async Task<PlayfieldInfo?> GetAsync(Guid playfieldId, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("GetPlayfieldInfo");
        activity?.SetTag("playfield.id", playfieldId);

        try
        {
            var request = _dapr.CreateInvokeMethodRequest(
                HttpMethod.Get,
                PlayfieldsAppId,
                $"internal/playfields/{playfieldId}");

            // Dapr 1.17 marks the HttpRequestMessage overload [Obsolete] in favour of native HTTP/gRPC
            // clients. We keep it deliberately: it is the only abstract (Moq-able) invocation overload,
            // and migrating off Dapr service invocation is an architectural change tracked separately.
#pragma warning disable CS0618
            var dto = await _dapr.InvokeMethodAsync<PlayFieldDto>(request, ct);
#pragma warning restore CS0618

            return dto is null
                ? null
                : new PlayfieldInfo(
                    dto.Name,
                    dto.Points.Select(p => new GamesGpsCoordinateDto(p.Latitude, p.Longitude)).ToList());
        }
        catch (InvocationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
