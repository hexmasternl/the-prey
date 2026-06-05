using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.GameEngine;

internal static class EligibilityEvaluator
{
    /// <summary>
    /// Returns participants due for a location broadcast at <paramref name="now"/>.
    /// A participant is eligible when they have never been broadcasted, or when the time elapsed
    /// since their last broadcast meets or exceeds their current reporting interval.
    /// </summary>
    internal static IReadOnlyList<GameParticipant> GetEligible(
        Game game,
        DateTimeOffset now,
        IReadOnlyDictionary<Guid, DateTimeOffset> lastBroadcastTimes)
    {
        var allParticipants = new List<GameParticipant>();
        if (game.Hunter is not null) allParticipants.Add(game.Hunter);
        allParticipants.AddRange(game.Preys);

        var eligible = new List<GameParticipant>();
        foreach (var participant in allParticipants)
        {
            var intervalSeconds = game.ReportingIntervalFor(participant.UserId, now);

            if (!lastBroadcastTimes.TryGetValue(participant.UserId, out var lastBroadcast))
            {
                eligible.Add(participant);
                continue;
            }

            if (now >= lastBroadcast.AddSeconds(intervalSeconds))
                eligible.Add(participant);
        }

        return eligible;
    }
}
