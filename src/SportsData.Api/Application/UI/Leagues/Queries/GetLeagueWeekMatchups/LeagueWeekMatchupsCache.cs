using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using SportsData.Api.Application.UI.Leagues.Dtos;
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

    public LeagueWeekMatchupsCache(
        IDistributedCache cache,
        ILogger<LeagueWeekMatchupsCache> logger)
    {
        _cache = cache;
        _logger = logger;
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
            return await _cache.GetRecordAsync<LeagueWeekMatchupsDto>(BuildKey(leagueId, week));
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
    /// </remarks>
    private static TimeSpan? ResolveTtl(LeagueWeekMatchupsDto dto)
    {
        if (dto.Matchups.Count == 0)
            return PregameTtl;

        var statuses = dto.Matchups.Select(m => m.Status).ToList();

        if (statuses.Any(s => !IsScheduled(s) && !IsFinal(s)))
            return null;

        return statuses.All(IsFinal)
            ? SettledTtl
            : PregameTtl;
    }

    private static bool IsScheduled(string? status) =>
        string.Equals(status, ScheduledStatus, StringComparison.OrdinalIgnoreCase);

    private static bool IsFinal(string? status) =>
        status is not null &&
        status.StartsWith(FinalStatus, StringComparison.OrdinalIgnoreCase);
}
