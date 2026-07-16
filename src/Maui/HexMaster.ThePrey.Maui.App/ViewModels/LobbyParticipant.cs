using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// One row in the lobby's participants list: the player's name, their role (derived from who the
/// designated hunter is), and their ready state. The boolean pairs drive the localized role/ready
/// labels' visibility in XAML without a converter.
/// </summary>
public sealed record LobbyParticipant(Guid UserId, string DisplayName, bool IsHunter, bool IsReady, bool IsOwner)
{
    public LobbyParticipant(GameParticipantDetails participant, Guid? hunterUserId, Guid ownerUserId)
        : this(
            participant.UserId,
            participant.DisplayName,
            participant.UserId == hunterUserId,
            participant.IsReady,
            participant.UserId == ownerUserId)
    {
    }

    /// <summary>True when this player is not the hunter (drives the "PREY" badge).</summary>
    public bool IsPrey => !IsHunter;

    /// <summary>
    /// Whether to show the "READY" badge. The game creator never has to ready up, so the ready
    /// indicator is suppressed entirely for the owner's own row (both READY and NOT READY).
    /// </summary>
    public bool ShowReady => !IsOwner && IsReady;

    /// <summary>Whether to show the "NOT READY" badge — suppressed for the owner (see <see cref="ShowReady"/>).</summary>
    public bool ShowNotReady => !IsOwner && !IsReady;
}
