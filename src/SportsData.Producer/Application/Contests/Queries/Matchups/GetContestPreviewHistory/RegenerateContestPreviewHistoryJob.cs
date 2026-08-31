using SportsData.Core.Common;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestPreviewHistory;

/// <summary>
/// Rebuilds a contest's cached preview history after its line moves.
/// </summary>
public interface IRegenerateContestPreviewHistoryJob
{
    Task RegenerateAsync(Guid contestId);
}

/// <inheritdoc />
public class RegenerateContestPreviewHistoryJob : IRegenerateContestPreviewHistoryJob
{
    private readonly ILogger<RegenerateContestPreviewHistoryJob> _logger;
    private readonly IGetContestPreviewHistoryQueryHandler _handler;

    public RegenerateContestPreviewHistoryJob(
        ILogger<RegenerateContestPreviewHistoryJob> logger,
        IGetContestPreviewHistoryQueryHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    /// <summary>
    /// Runs the query, which populates the cache entry for the contest's CURRENT line.
    /// </summary>
    /// <remarks>
    /// The cache is keyed by the spread, so a line move produces a new key that nothing
    /// has populated yet. Left alone, the next person to open the matchup dialog would
    /// pay the full ~10 round trips to fill it. Running it here, off the request path and
    /// immediately after the odds are persisted, means the entry is already warm by the
    /// time anyone asks.
    /// <para>
    /// This closes the one miss that is caused by the DATA changing. Line movement is the
    /// only content change that invalidates an entry, and it already publishes an event,
    /// so reacting to it removes the window in which a reader would rebuild after a line
    /// move.
    /// </para>
    /// <para>
    /// It does not make misses impossible, and nothing here should be read as claiming so.
    /// A reader still rebuilds when an entry simply is not there: the seven-day expiry on
    /// a resolved contest, the five-minute expiry on one whose id did not resolve (so a
    /// request arriving before the contest is sourced re-checks shortly after), eviction
    /// under Redis's allkeys-lru policy when memory is tight, a Redis restart (the cache
    /// has no persistence, by design), or a deliberate payload-version bump. Those are
    /// lifecycle events rather than staleness, and rebuilding on them is the correct
    /// behaviour — it is also exactly what happened before any of this caching existed.
    /// </para>
    /// <para>
    /// Failures are logged and swallowed. This is a cache warm — if it does not run, the
    /// next reader simply recomputes, which is the behaviour we had before. Throwing would
    /// hand a retry storm to Hangfire in exchange for nothing.
    /// </para>
    /// </remarks>
    public async Task RegenerateAsync(Guid contestId)
    {
        try
        {
            var result = await _handler.ExecuteAsync(
                new GetContestPreviewHistoryQuery(contestId),
                CancellationToken.None);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Preview history regenerated after line move. ContestId={ContestId}",
                    contestId);

                return;
            }

            _logger.LogWarning(
                "Preview history regeneration returned no result. ContestId={ContestId}",
                contestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Preview history regeneration failed. ContestId={ContestId}. The next reader will recompute.",
                contestId);
        }
    }
}
