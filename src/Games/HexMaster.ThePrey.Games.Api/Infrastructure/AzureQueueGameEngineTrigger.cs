using Azure.Storage.Queues;
using HexMaster.ThePrey.Games;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Api.Infrastructure;

internal sealed class AzureQueueGameEngineTrigger : IGameEngineTrigger
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly string _queueName;
    private readonly ILogger<AzureQueueGameEngineTrigger> _logger;

    public AzureQueueGameEngineTrigger(
        QueueServiceClient queueServiceClient,
        IConfiguration configuration,
        ILogger<AzureQueueGameEngineTrigger> logger)
    {
        _queueServiceClient = queueServiceClient;
        _queueName = configuration["GameEngine:QueueName"] ?? "game-engine-queue";
        _logger = logger;
    }

    public async Task TriggerAsync(Guid gameId, CancellationToken ct)
    {
        var client = _queueServiceClient.GetQueueClient(_queueName);
        await client.CreateIfNotExistsAsync(cancellationToken: ct);

        var message = BinaryData.FromObjectAsJson(new { GameId = gameId });
        await client.SendMessageAsync(message, cancellationToken: ct);

        _logger.LogInformation("Game engine triggered for game {GameId}", gameId);
    }
}
