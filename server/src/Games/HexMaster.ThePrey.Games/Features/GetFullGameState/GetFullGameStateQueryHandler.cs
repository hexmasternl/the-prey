using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.GetFullGameState;

/// <summary>
/// Builds the one-call composite snapshot of a game for a single caller. Composes the same slices the
/// three separate reads return — the game record, the playfield, the in-progress status, and the
/// caller's role state — from one aggregate load, so every part of the response is internally
/// consistent (the three-call version could interleave with a sweep tick and mix two generations).
/// </summary>
public sealed class GetFullGameStateQueryHandler : IQueryHandler<GetFullGameStateQuery, FullGameStateDto?>
{
    private static readonly IReadOnlyList<GpsCoordinateDto> NoCoordinates = [];

    private readonly IGameRepository _games;
    private readonly IPlayfieldInfoProvider _playfields;
    private readonly TimeProvider _timeProvider;

    public GetFullGameStateQueryHandler(
        IGameRepository games,
        IPlayfieldInfoProvider playfields,
        TimeProvider timeProvider)
    {
        _games = games;
        _playfields = playfields;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns <c>null</c> when the game does not exist (the endpoint maps that to 404), and throws
    /// <see cref="UnauthorizedAccessException"/> when the caller cannot see the game (mapped to 403).
    /// </summary>
    public async Task<FullGameStateDto?> Handle(GetFullGameStateQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = GameActivitySource.Source.StartActivity("GetFullGameState");
        activity?.SetTag("game.id", query.GameId);

        try
        {
            var game = await _games.GetByIdAsync(query.GameId, ct);
            if (game is null)
                return null;

            if (!game.IsVisibleTo(query.UserId))
                throw new UnauthorizedAccessException("Only members of the game can read its state.");

            activity?.SetTag("game.status", game.Status.ToString());

            var now = _timeProvider.GetUtcNow();
            var playfieldInfo = await _playfields.GetAsync(game.PlayfieldId, ct);

            // The two in-progress-only slices. Unlike GET /games/{id}/status, a non-in-progress game is
            // not an error here — the lobby and the outcome screen are served by the same endpoint.
            var isInProgress = game.Status == GameStatus.InProgress;
            var status = isInProgress ? game.ToStatusDto(playfieldInfo, query.UserId, now) : null;
            var roleState = isInProgress ? game.ToRoleStateDto(query.UserId) : null;

            activity?.SetTag("game.state.has_status", status is not null);
            activity?.SetTag("game.state.has_role_state", roleState is not null);

            return new FullGameStateDto(
                game.ToDto(query.UserId),
                playfieldInfo?.Name ?? string.Empty,
                playfieldInfo?.Coordinates ?? NoCoordinates,
                status,
                roleState,
                now);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
