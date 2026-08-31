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
    /// This is why the preview-history cache does not need a recurring warm job or a
    /// short expiry: line movement is the only thing that invalidates it, and line
    /// movement already publishes an event. React to that and there is no window in which
    /// a user can take the hit.
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
