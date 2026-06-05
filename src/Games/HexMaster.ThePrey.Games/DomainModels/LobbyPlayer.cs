namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A player who has joined a game's lobby. Identified by <see cref="UserId"/>; carries a display name,
/// an optional profile picture URL, and a ready-up flag.
/// </summary>
public sealed record LobbyPlayer(Guid UserId, string DisplayName, string? ProfilePictureUrl, bool IsReady = false)
{
    public static LobbyPlayer Create(Guid userId, string displayName, string? profilePictureUrl = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A lobby player requires a non-empty user identifier.", nameof(userId));

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new LobbyPlayer(userId, displayName, string.IsNullOrWhiteSpace(profilePictureUrl) ? null : profilePictureUrl);
    }

    public LobbyPlayer WithReady(bool isReady) => this with { IsReady = isReady };
}
