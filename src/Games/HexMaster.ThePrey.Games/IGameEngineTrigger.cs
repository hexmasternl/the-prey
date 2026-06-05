namespace HexMaster.ThePrey.Games;

/// <summary>
/// Port for triggering the game engine when a game starts.
/// The implementation lives in the API project and enqueues a message to Azure Storage Queue.
/// </summary>
public interface IGameEngineTrigger
{
    Task TriggerAsync(Guid gameId, CancellationToken ct);
}
