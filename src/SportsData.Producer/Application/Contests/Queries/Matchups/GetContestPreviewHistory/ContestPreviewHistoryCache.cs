using Microsoft.Extensions.Caching.Distributed;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

using System;
using System.Globalization;
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
/// specific to this query.
/// </remarks>
public interface IContestPreviewHistoryCache
{
    /// <param name="homeSpread">
    /// The contest's current home spread, or null when it has no line. Part of the key —
    /// see <see cref="ContestPreviewHistoryCache.BuildKey"/>.
    /// </param>
    Task<ContestPreviewHistoryDto?> GetAsync(
        GetContestPreviewHistoryQuery query,
        double? homeSpread);

    /// <param name="contestStartUtc">
    /// Kickoff, or null when the contest id resolved to nothing.
    /// </param>
    /// <param name="homeSpread">
    /// The contest's current home spread, or null when it has no line.
    /// </param>
    Task SetAsync(
        GetContestPreviewHistoryQuery query,
        ContestPreviewHistoryDto dto,
        DateTime? contestStartUtc,
        double? homeSpread);
}

/// <inheritdoc />
public sealed class ContestPreviewHistoryCache : IContestPreviewHistoryCache
{
    /// <summary>
    /// Lifetime for a resolved contest. Long, because the line is in the key rather than
    /// governed by expiry — see <see cref="ResolveTtl"/>.
    /// </summary>
    private static readonly TimeSpan ResolvedTtl = TimeSpan.FromDays(7);

    /// <summary>
    /// Lifetime for a contest id that resolved to nothing. Brief, so a request arriving
    /// before the contest is sourced cannot pin an empty result for long.
    /// </summary>
    private static readonly TimeSpan UnresolvedContestTtl = TimeSpan.FromMinutes(5);

    private readonly IDistributedCache _cache;
    private readonly ILogger<ContestPreviewHistoryCache> _logger;

    public ContestPreviewHistoryCache(
        IDistributedCache cache,
        ILogger<ContestPreviewHistoryCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Returns the cached payload, or null on a miss — including when Redis itself fails.
    /// </summary>
    /// <remarks>
    /// A cache must never be able to fail the request it is supposed to accelerate. An
    /// unreachable Redis degrades this slice to the ~10 round trips it cost before any
    /// caching existed, which is slow but correct; letting the exception escape would
    /// turn an optimisation into an outage. Same fail-open stance as
    /// <c>RedisEspnCircuitBreaker</c>.
    /// </remarks>
    public async Task<ContestPreviewHistoryDto?> GetAsync(
        GetContestPreviewHistoryQuery query,
        double? homeSpread)
    {
        try
        {
            return await _cache.GetRecordAsync<ContestPreviewHistoryDto>(BuildKey(query, homeSpread));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Preview history cache read failed; falling back to the uncached path. ContestId={ContestId}",
                query.ContestId);

            return null;
        }
    }

    public async Task SetAsync(
        GetContestPreviewHistoryQuery query,
        ContestPreviewHistoryDto dto,
        DateTime? contestStartUtc,
        double? homeSpread)
    {
        try
        {
            await _cache.SetRecordAsync(
                BuildKey(query, homeSpread),
                dto,
                ResolveTtl(contestStartUtc));
        }
        catch (Exception ex)
        {
            // Swallowed deliberately. The caller already has the payload and is about to
            // return it successfully; failing to memoise it is not a reason to hand the
            // user an error for data we are holding.
            _logger.LogWarning(
                ex,
                "Preview history cache write failed; the response is unaffected. ContestId={ContestId}",
                query.ContestId);
        }
    }

    /// <summary>
    /// Key for one preview-history result.
    /// </summary>
    /// <remarks>
    /// The home spread is part of the key, and that is what makes a long lifetime safe.
    /// Every other input is settled history — past meetings, prior-season records — so the
    /// only thing that can make a cached answer wrong is the line moving. Keying on it
    /// means a move produces a different key and the answer is recomputed, instead of a
    /// timer racing the sportsbook.
    /// <para>
    /// The SIGNED value, not the magnitude: a line crossing zero swaps favorite and
    /// underdog, so -3 and +3 are different answers, not the same one.
    /// </para>
    /// <para>
    /// MeetingCount and RecentGameCount are included because the query exposes both, and
    /// two callers asking for different depths must not share an entry. Every caller uses
    /// the 5/5 defaults today, which is precisely why keying on contest id alone would
    /// have looked correct indefinitely.
    /// </para>
    /// <para>
    /// v1 is a payload version — changing ContestPreviewHistoryDto invalidates every entry
    /// by bumping it, rather than feeding old shapes to new deserializers.
    /// </para>
    /// </remarks>
    private static string BuildKey(GetContestPreviewHistoryQuery query, double? homeSpread)
    {
        var spread = homeSpread?.ToString("0.##", CultureInfo.InvariantCulture) ?? "none";

        // v2: ATS-pair distance guard (2026-09-01) changed the payload for
        // wide spreads; the version bump retires 7-day-TTL v1 entries that
        // would otherwise serve the stretched "35+ favorite" bullets.
        return $"preview-history:v2:{query.ContestId}:{query.MeetingCount}:{query.RecentGameCount}:s{spread}";
    }

    /// <summary>
    /// How long a result stays true.
    /// </summary>
    /// <remarks>
    /// Long for any contest we could resolve. Correctness against a moving line is handled
    /// by the key, not by expiry, so the TTL exists only as a backstop against unbounded
    /// growth and against slow drift in inputs we do not key on.
    /// <para>
    /// This endpoint is read almost entirely BEFORE kickoff, while people are deciding
    /// picks; after the game nobody opens it. An expiry short enough to keep a line honest
    /// would therefore have expired during the only window that matters — which is why the
    /// line moved into the key instead.
    /// </para>
    /// </remarks>
    private static TimeSpan ResolveTtl(DateTime? contestStartUtc) =>
        contestStartUtc is null
            ? UnresolvedContestTtl
            : ResolvedTtl;
}
