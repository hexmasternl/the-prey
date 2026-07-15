using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// One row in the lobby's participants list: the player's name, their role (derived from who the
/// designated hunter is), and their ready state. The boolean pairs drive the localized role/ready
/// labels' visibility in XAML without a converter.
/// </summary>
public sealed record LobbyParticipant(Guid UserId, string DisplayName, bool IsHunter, bool IsReady)
{
    public LobbyParticipant(GameParticipantDetails participant, Guid? hunterUserId)
        : this(participant.UserId, participant.DisplayName, participant.UserId == hunterUserId, participant.IsReady)
    {
    }

    /// <summary>True when this player is not the hunter (drives the "PREY" badge).</summary>
    public bool IsPrey => !IsHunter;

    /// <summary>True when this player has not readied up (drives the "NOT READY" badge).</summary>
    public bool IsNotReady => !IsReady;
}
