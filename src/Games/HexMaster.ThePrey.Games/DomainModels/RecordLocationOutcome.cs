namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// The result of recording a participant location: the participant's state before the call and,
/// when the hunter moved during the head-start delay, the penalty that was applied.
/// </summary>
public sealed record RecordLocationOutcome(PlayerState PreviousState, Penalty? DelayViolationPenalty);
