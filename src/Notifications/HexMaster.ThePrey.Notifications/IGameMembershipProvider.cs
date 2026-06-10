namespace HexMaster.ThePrey.Notifications;

/// <summary>
/// Checks whether a user may receive a game's real-time notifications (i.e. is its owner or a
/// participant). Implemented via a Dapr service invocation to the Games module.
/// </summary>
public interface IGameMembershipProvider
{
    Task<bool> IsMemberAsync(Guid gameId, Guid userId, CancellationToken ct);
}
