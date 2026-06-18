using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.GetTagCandidates;

/// <summary>
/// Returns the preys the hunter is currently within range to tag. The hunter and each prey are judged
/// by their most recent emitted location; only preys within <see cref="Game.TagRangeMeters"/> are returned.
/// </summary>
public sealed class GetTagCandidatesQueryHandler : IQueryHandler<GetTagCandidatesQuery, TagCandidatesDto?>
{
    private readonly IGameRepository _games;

    public GetTagCandidatesQueryHandler(IGameRepository games)
    {
        _games = games;
    }

    public async Task<TagCandidatesDto?> Handle(GetTagCandidatesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = GameActivitySource.Source.StartActivity("GetTagCandidates");
        activity?.SetTag("game.id", query.GameId);

        try
        {
            var game = await _games.GetByIdAsync(query.GameId, ct);
            if (game is null)
                return null;

            if (game.HunterUserId != query.CallerId)
                throw new UnauthorizedAccessException("Only the hunter can retrieve tag candidates.");

            var candidates = game.TaggablePreysWithin(Game.TagRangeMeters)
                .Select(c => new TagCandidateDto(
                    c.Prey.UserId,
                    c.Prey.DisplayName,
                    c.Prey.State.ToString(),
                    c.DistanceMeters))
                .ToList();

            activity?.SetTag("game.tag.candidate_count", candidates.Count);

            return new TagCandidatesDto(Game.TagRangeMeters, candidates);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
