using System.Diagnostics;
using Azure.Storage.Queues;
using HexMaster.ThePrey.GameEngine.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThePrey.Aspire.ServiceDefaults;

namespace HexMaster.ThePrey.GameEngine;

internal sealed class GameEngineWorker : BackgroundService
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly GameLocationChecker _locationChecker;
    private readonly string _queueName;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<GameEngineWorker> _logger;

    public GameEngineWorker(
        QueueServiceClient queueServiceClient,
        GameLocationChecker locationChecker,
        IConfiguration configuration,
        IHostApplicationLifetime appLifetime,
        ILogger<GameEngineWorker> logger)
    {
        _queueServiceClient = queueServiceClient;
        _locationChecker = locationChecker;
        _queueName = configuration["GameEngine:QueueName"] ?? AspireConstants.Queues.GameStart;
        _appLifetime = appLifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = GameEngineActivitySource.Source.StartActivity("GameEngine.Execute");

        try
        {
            var queueClient = _queueServiceClient.GetQueueClient(_queueName);
            await queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

            var message = await ReceiveMessageAsync(queueClient, stoppingToken);
            if (message is null)
            {
                _logger.LogWarning("No message received from queue; engine exiting");
                return;
            }

            var gameId = ParseGameId(message.MessageText);
            if (gameId == Guid.Empty)
            {
                _logger.LogError("Invalid or empty gameId in queue message: {Message}", message.MessageText);
                await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                return;
            }

            activity?.SetTag("game.id", gameId);
            _logger.LogInformation("Game engine received message for game {GameId}", gameId);

            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);

            await _locationChecker.RunAsync(gameId, stoppingToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Game engine encountered an unhandled error");
            // Non-zero exit so the Container Apps Job execution is marked Failed.
            Environment.ExitCode = 1;
            throw;
        }
        finally
        {
            // This worker runs as a Container Apps Job: it processes a single game and must
            // exit. A completed BackgroundService does not stop the Generic Host on its own,
            // so without this the replica would idle until replicaTimeout and be marked Failed.
            // Stopping here lets the execution complete as soon as the game ends.
            _appLifetime.StopApplication();
        }
    }

    private static async Task<Azure.Storage.Queues.Models.QueueMessage?> ReceiveMessageAsync(
        QueueClient queueClient,
        CancellationToken ct)
    {
        var response = await queueClient.ReceiveMessageAsync(cancellationToken: ct);
        return response?.Value;
    }

    private static Guid ParseGameId(string messageText)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(messageText);
            if (doc.RootElement.TryGetProperty("GameId", out var prop) &&
                prop.TryGetGuid(out var id))
                return id;
        }
        catch
        {
            // swallow parse errors; caller handles empty guid
        }

        return Guid.Empty;
    }
}
