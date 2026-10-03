using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;

/// <summary>
/// Caching policy for a league's week of matchups: key shape, entry lifetime, and
/// — most importantly — when NOT to cache at all.
/// </summary>
/// <remarks>
/// Lives in the slice rather than shared infrastructure because the rules below are
/// statements about football game states, not something another feature would reuse.
/// </remarks>
public interface ILeagueWeekMatchupsCache
{
    Task<LeagueWeekMatchupsDto?> GetAsync(Guid leagueId, int week);

    /// <summary>
    /// Stores the payload if — and only if — its contents are safe to serve again.
    /// A no-op while any contest in the week is live.
    /// </summary>
    Task SetAsync(Guid leagueId, int week, LeagueWeekMatchupsDto dto);

    /// <summary>
    /// Drops the entry for a league-week. Call after anything that changes the
    /// week's slate (matchup generation, refresh, manual add) so the next read
    /// rebuilds it instead of serving the pre-change payload for the rest of
    /// its TTL.
    /// </summary>
    Task RemoveAsync(Guid leagueId, int week);
}

/// <inheritdoc />
public sealed class LeagueWeekMatchupsCache : ILeagueWeekMatchupsCache
{
    /// <summary>Every contest finished. Results are frozen.</summary>
    private static readonly TimeSpan SettledTtl = TimeSpan.FromMinutes(30);

    /// <summary>Nothing has kicked off yet. Metadata drifts slowly; spreads move but not by the minute.</summary>
    private static readonly TimeSpan PregameTtl = TimeSpan.FromMinutes(5);

    private const string ScheduledStatus = "STATUS_SCHEDULED";
    private const string FinalStatus = "STATUS_FINAL";

    private readonly IDistributedCache _cache;
    private readonly ILogger<LeagueWeekMatchupsCache> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public LeagueWeekMatchupsCache(
        IDistributedCache cache,
        ILogger<LeagueWeekMatchupsCache> logger,
        IDateTimeProvider dateTimeProvider)
    {
        _cache = cache;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Returns the cached payload, or null on a miss — including when Redis itself fails.
    /// </summary>
    /// <remarks>
    /// A cache must never be able to fail the request it is supposed to accelerate. This
    /// is the same fail-open stance <c>RedisEspnCircuitBreaker</c> takes: an unreachable
    /// Redis degrades to the behaviour we had before any caching existed, rather than
    /// turning an optimisation into an outage on the most-used endpoint in the app.
    /// </remarks>
    public async Task<LeagueWeekMatchupsDto?> GetAsync(Guid leagueId, int week)
    {
        try
        {
            var cached = await _cache.GetRecordAsync<LeagueWeekMatchupsDto>(BuildKey(leagueId, week));

            // A payload that was safe to write can stop being safe purely by the
            // passage of time: it was cached while everything was scheduled, and
            // then a game kicked off. The write side caps the TTL at the next
            // kickoff so this should not happen, but expiry is not instant and an
            // entry can outlive the pod version that wrote it. Re-checking on read
            // makes the invariant hold at the moment it actually matters.
            if (cached is not null && KickoffHasPassed(cached))
            {
                // Evict rather than just ignore. Without this every request for the
                // rest of the entry's natural lifetime re-reads and re-deserializes
                // a payload we have already judged unusable, and logs a line doing
                // it — a burst per member per request across a kickoff wave.
                await _cache.RemoveAsync(BuildKey(leagueId, week));

                _logger.LogInformation(
                    "League week matchups cache entry discarded: a contest has kicked off since it was written. leagueId={LeagueId}, week={Week}",
                    leagueId,
                    week);

                return null;
            }

            return cached;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "League week matchups cache read failed; falling back to the uncached path. leagueId={LeagueId}, week={Week}",
                leagueId,
                week);

            return null;
        }
    }

    public async Task SetAsync(Guid leagueId, int week, LeagueWeekMatchupsDto dto)
    {
        var ttl = ResolveTtl(dto);

        if (ttl is null)
            return;

        try
        {
            await _cache.SetRecordAsync(BuildKey(leagueId, week), dto, ttl.Value);
        }
        catch (Exception ex)
        {
            // Swallowed deliberately. The caller already has the payload and is about to
            // return it successfully; failing to memoise it is not a reason to hand the
            // user an error for data we are holding.
            _logger.LogWarning(
                ex,
                "League week matchups cache write failed; the response is unaffected. leagueId={LeagueId}, week={Week}",
                leagueId,
                week);
        }
    }

    public async Task RemoveAsync(Guid leagueId, int week)
    {
        try
        {
            await _cache.RemoveAsync(BuildKey(leagueId, week));
        }
        catch (Exception ex)
        {
            // Same fail-open stance as the read and write sides: the slate change that
            // triggered this eviction is already committed, and a Redis hiccup must not
            // fault it. Worst case the old entry lives out its TTL.
            _logger.LogWarning(
                ex,
                "League week matchups cache eviction failed; the entry will expire on its own. leagueId={LeagueId}, week={Week}",
                leagueId,
                week);
        }
    }

    /// <summary>
    /// Key is league + week only.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT keyed by user. The query takes a UserId, but it is used solely
    /// for the membership guard and logging — no per-user pick data reaches this payload.
    /// One entry therefore serves every member of the league, which is the entire point.
    /// If per-user content is ever added here, this key becomes a data-leakage bug and
    /// must gain the user id (or the caching must be removed).
    /// </remarks>
    private static string BuildKey(Guid leagueId, int week) =>
        $"league-week-matchups:v1:{leagueId}:{week}";

    /// <summary>
    /// Lifetime for this payload, or null to mean "do not cache".
    /// </summary>
    /// <remarks>
    /// This payload carries live game state — Status, Period, Clock, AwayScore,
    /// HomeScore — merged in from Producer. Serving a cached copy while a game is in
    /// progress would freeze the scoreboard on the surface users are watching precisely
    /// because it is changing. So we do not cache at all while anything is live: during
    /// game windows the behaviour is identical to having no cache.
    /// <para>
    /// That costs nothing. Under load the database is not the constraint here — measured
    /// at one active Postgres connection against fifty concurrent users — so the win from
    /// caching is in the long tail of pre- and post-game browsing, which is exactly what
    /// this still covers.
    /// </para>
    /// <para>
    /// Unrecognised or missing statuses are treated as live. Failing closed means a new
    /// or unexpected ESPN status degrades to today's behaviour rather than silently
    /// pinning a stale scoreboard.
    /// </para>
    /// <para>
    /// Status alone is not enough, because status describes the payload at the moment it
    /// was written and the TTL then outlives that moment. A slate cached at 3:29 with
    /// every game STATUS_SCHEDULED stayed servable until 3:34 even though kickoff was
    /// 3:30 — so a cold load in that window rendered an in-progress game as scheduled,
    /// with no score, and nothing corrected it until the next SignalR push (which at
    /// halftime or between quarters can be a long wait). Every kickoff wave re-armed the
    /// window. The lifetime is therefore also capped at the next kickoff, and a
    /// not-yet-final contest whose kickoff has already passed is not cached at all.
    /// </para>
    /// </remarks>
    private TimeSpan? ResolveTtl(LeagueWeekMatchupsDto dto)
    {
        // An empty slate is the one payload that is about to change: the week has
        // just been created, wiped for regeneration, or not generated yet. Caching
        // it pinned "no matchups" for five minutes after a regeneration on
        // 2026-09-14. Empty is also the cheapest response to rebuild.
        if (dto.Matchups.Count == 0)
            return null;

        var statuses = dto.Matchups.Select(m => m.Status).ToList();

        if (statuses.Any(s => !IsScheduled(s) && !IsFinal(s)))
            return null;

        if (statuses.All(IsFinal))
            return SettledTtl;

        // Something is still to come. A contest reading SCHEDULED past its own kickoff
        // is the stale-scoreboard case itself: either it has started and the status row
        // has not caught up, or it is postponed and its start time is meaningless. Both
        // are payloads we must not serve twice. Refusing to cache is cheap here — the
        // uncached path is the documented fallback, not a cliff.
        if (KickoffHasPassed(dto))
            return null;

        var untilNextKickoff = dto.Matchups
            .Where(m => !IsFinal(m.Status))
            .Min(m => m.StartDateUtc) - _dateTimeProvider.UtcNow();

        return untilNextKickoff < PregameTtl
            ? untilNextKickoff
            : PregameTtl;
    }

    /// <summary>
    /// True when some contest that has not finished is already past its kickoff, so the
    /// payload's scoreboard is either moving now or about to.
    /// </summary>
    private bool KickoffHasPassed(LeagueWeekMatchupsDto dto)
    {
        var now = _dateTimeProvider.UtcNow();

        return dto.Matchups.Any(m => !IsFinal(m.Status) && m.StartDateUtc <= now);
    }

    private static bool IsScheduled(string? status) =>
        string.Equals(status, ScheduledStatus, StringComparison.OrdinalIgnoreCase);

    private static bool IsFinal(string? status) =>
        status is not null &&
        status.StartsWith(FinalStatus, StringComparison.OrdinalIgnoreCase);
}
