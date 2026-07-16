namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// A typed in-game real-time event delivered over the game's Azure Web PubSub group channel. The
/// transport/envelope details (WebSocket, <c>joinGroup</c>, <c>{ type, data }</c> frames, reconnect)
/// live in the <see cref="IGameStreamClient"/> implementation; the gameplay view models consume only
/// this small discriminated set, so they stay transport-agnostic and unit-testable against a fake
/// channel emitting scripted events.
/// </summary>
public abstract record GameStreamEvent
{
    private GameStreamEvent() { }

    /// <summary>A participant's latest broadcast position (<c>player-location-updated</c>).</summary>
    public sealed record ParticipantLocated(Guid UserId, double Latitude, double Longitude, string? State) : GameStreamEvent;

    /// <summary>A participant transitioned to <paramref name="NewState"/> (<c>participant-/player-status-changed</c>).</summary>
    public sealed record ParticipantStatusChanged(Guid ParticipantId, string NewState) : GameStreamEvent;

    /// <summary>The game moved to <paramref name="NewState"/> (<c>state-changed</c>, e.g. Ready→InProgress).</summary>
    public sealed record StateChanged(string NewState) : GameStreamEvent;

    /// <summary>The game concluded (<c>game-ended</c>), with an optional outcome and survivor count.</summary>
    public sealed record GameEnded(string? Outcome, int? SurvivorCount) : GameStreamEvent;
}
