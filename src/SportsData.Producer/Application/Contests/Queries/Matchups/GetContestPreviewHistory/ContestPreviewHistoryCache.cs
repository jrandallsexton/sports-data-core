using Microsoft.Extensions.Caching.Distributed;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

using System;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestPreviewHistory;

/// <summary>
/// Caching policy for preview history: key shape and entry lifetime.
/// </summary>
/// <remarks>
/// Separate from the handler on purpose. The handler's job is deciding WHAT the
/// preview history is; how long an answer stays true, and how it is addressed in a
/// key-value store, is a different concern and reads as noise inside it. It lives in
/// this slice rather than a shared infrastructure namespace because the policy is
/// specific to this query — the lifetime rule below is a statement about spreads and
/// kickoff times, not something another feature would reuse.
/// </remarks>
public interface IContestPreviewHistoryCache
{
    Task<ContestPreviewHistoryDto?> GetAsync(GetContestPreviewHistoryQuery query);

    /// <param name="contestStartUtc">
    /// Kickoff for the contest, or null when the id resolved to nothing. Drives the
    /// entry lifetime — see the implementation.
    /// </param>
    Task SetAsync(
        GetContestPreviewHistoryQuery query,
        ContestPreviewHistoryDto dto,
        DateTime? contestStartUtc);
}

/// <inheritdoc />
public sealed class ContestPreviewHistoryCache : IContestPreviewHistoryCache
{
    /// <summary>Lifetime once the contest's inputs have frozen.</summary>
    private static readonly TimeSpan SettledTtl = TimeSpan.FromDays(7);

    /// <summary>Lifetime while the contest is still ahead of us — short, because the spread moves.</summary>
    private static readonly TimeSpan LiveTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Lifetime for a contest id that resolved to nothing. Brief, so a request arriving
    /// before the contest is sourced cannot pin an empty result for long.
    /// </summary>
    private static readonly TimeSpan UnresolvedContestTtl = TimeSpan.FromMinutes(5);

    /// <summary>How long after kickoff the inputs are considered frozen.</summary>
    private static readonly TimeSpan SettlesAfterStart = TimeSpan.FromHours(24);

    private readonly IDistributedCache _cache;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ContestPreviewHistoryCache(
        IDistributedCache cache,
        IDateTimeProvider dateTimeProvider)
    {
        _cache = cache;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<ContestPreviewHistoryDto?> GetAsync(GetContestPreviewHistoryQuery query) =>
        _cache.GetRecordAsync<ContestPreviewHistoryDto>(BuildKey(query));

    public Task SetAsync(
        GetContestPreviewHistoryQuery query,
        ContestPreviewHistoryDto dto,
        DateTime? contestStartUtc) =>
        _cache.SetRecordAsync(BuildKey(query), dto, ResolveTtl(contestStartUtc));

    /// <summary>
    /// Key for one preview-history result.
    /// </summary>
    /// <remarks>
    /// MeetingCount and RecentGameCount are included deliberately: the query exposes
    /// both, so two callers asking for different depths must not share an entry. Every
    /// caller happens to use the 5/5 defaults today, which is precisely why keying on
    /// contest id alone would have looked correct indefinitely.
    /// <para>
    /// The v1 segment is a payload version — changing ContestPreviewHistoryDto
    /// invalidates every entry by bumping it, rather than feeding old shapes to new
    /// deserializers until they expire.
    /// </para>
    /// </remarks>
    private static string BuildKey(GetContestPreviewHistoryQuery query) =>
        $"preview-history:v1:{query.ContestId}:{query.MeetingCount}:{query.RecentGameCount}";

    /// <summary>
    /// How long a result stays true.
    /// </summary>
    /// <remarks>
    /// Almost everything in the payload is genuinely historical — head-to-head meetings
    /// and prior-season records are settled facts. The exception is the spread: the
    /// handler's spread context reads the contest's CURRENT line and derives favorite,
    /// underdog, magnitude and the ATS key-number bucket from it, and lines move through
    /// the week. A multi-day entry for an upcoming game would therefore serve a stale
    /// line dressed up as historical fact, which is the one way this cache could
    /// actively mislead rather than merely lag.
    /// <para>
    /// Hence short until the game is well past kickoff, long afterwards. An hour still
    /// collapses thousands of user views into a single computation, which is where
    /// nearly all the benefit lives.
    /// </para>
    /// </remarks>
    private TimeSpan ResolveTtl(DateTime? contestStartUtc)
    {
        if (contestStartUtc is null)
            return UnresolvedContestTtl;

        var settlesAt = contestStartUtc.Value.Add(SettlesAfterStart);

        return _dateTimeProvider.UtcNow() >= settlesAt
            ? SettledTtl
            : LiveTtl;
    }
}
