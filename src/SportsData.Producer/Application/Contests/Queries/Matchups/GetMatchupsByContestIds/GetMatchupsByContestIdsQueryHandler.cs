using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;
using SportsData.Producer.Infrastructure.Sql;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsByContestIds;

public interface IGetMatchupsByContestIdsQueryHandler
{
    Task<Result<List<LeagueMatchupDto>>> ExecuteAsync(
        GetMatchupsByContestIdsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupsByContestIdsQueryHandler : IGetMatchupsByContestIdsQueryHandler
{
    private readonly ILogger<GetMatchupsByContestIdsQueryHandler> _logger;
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetMatchupsByContestIdsQueryHandler(
        ILogger<GetMatchupsByContestIdsQueryHandler> logger,
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<LeagueMatchupDto>>> ExecuteAsync(
        GetMatchupsByContestIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.ContestIds.Length == 0)
        {
            return new Success<List<LeagueMatchupDto>>(new List<LeagueMatchupDto>());
        }

        // Timed so the Producer leg of a cross-service trace is legible in
        // Seq: these events carry the same @TraceId as the API request that
        // called us (W3C traceparent via HttpClient instrumentation).
        var totalTimer = System.Diagnostics.Stopwatch.StartNew();

        var sql = _sqlProvider.GetMatchupsByContestIds();

        // Direction is the lowercase enum name ("roundel" / "shield" / "hex")
        // so it matches the Rel-tag convention used by the marks batch script.
        // The SQL's CASE-based ORDER BY checks Rel @> ARRAY['sportdeets-mark', @Direction].
        var directionTag = query.Direction.ToString().ToLowerInvariant();

        var connection = _dbContext.Database.GetDbConnection();
        var matchups = (await connection.QueryAsync<LeagueMatchupDto>(
            new CommandDefinition(
                sql,
                new { ContestIds = query.ContestIds, Direction = directionTag },
                cancellationToken: cancellationToken)))
            .ToList();

        var sqlMs = totalTimer.ElapsedMilliseconds;

        var streamTimes = await GetActiveStreamTimesAsync(query.ContestIds, cancellationToken);
        var probables = await GetProbablePitchersAsync(query.ContestIds, cancellationToken);
        var seriesSummaries = await GetCurrentSeriesSummariesAsync(query.ContestIds, cancellationToken);

        // Snap state exists only for games currently being played, but this
        // used to load EVERY play of EVERY requested contest — for a
        // completed 32-game NCAA week that's thousands of rows fetched per
        // request to decorate cards that never render a situation line.
        // Restrict the stitch to contests that aren't terminal or pre-game;
        // unknown/null statuses fail OPEN into the stitch so a live game
        // with a missing status row still gets its snap state.
        var liveEligibleContestIds = FilterLiveEligibleContestIds(matchups);
        var situations = liveEligibleContestIds.Length == 0
            ? new Dictionary<Guid, LiveSituation>()
            : await GetLiveSituationsAsync(liveEligibleContestIds, cancellationToken);
        foreach (var matchup in matchups)
        {
            matchup.StreamScheduledTimeUtc = streamTimes.GetValueOrDefault(matchup.ContestId);

            if (probables.TryGetValue(matchup.ContestId, out var pair))
            {
                matchup.HomeProbablePitcher = pair.Home;
                matchup.AwayProbablePitcher = pair.Away;
            }

            matchup.CurrentSeriesSummary = seriesSummaries.GetValueOrDefault(matchup.ContestId);

            if (situations.TryGetValue(matchup.ContestId, out var situation))
            {
                matchup.Down = situation.Down;
                matchup.Distance = situation.Distance;
                matchup.BallOnYardLine = situation.BallOnYardLine;

                // Overrides the shared SQL's start-of-play possession for
                // FOOTBALL only: the end-of-play team is who lines up next.
                // Baseball keeps the SQL value — the team credited with the
                // last play is the batting side, and baseball plays carry no
                // end-team column.
                if (situation.PossessionFranchiseSeasonId is not null)
                {
                    matchup.PossessionFranchiseSeasonId = situation.PossessionFranchiseSeasonId;
                }
            }
        }

        _logger.LogInformation(
            "Canonical matchups served. Requested={RequestedCount}, Returned={ReturnedCount}, SqlMs={SqlMs}, TotalMs={TotalMs}",
            query.ContestIds.Length,
            matchups.Count,
            sqlMs,
            totalTimer.ElapsedMilliseconds);

        return new Success<List<LeagueMatchupDto>>(matchups);
    }

    /// <summary>
    /// Statuses that can never carry live snap state: the game hasn't
    /// started, or is over and will never produce another play. An
    /// EXCLUSION list on purpose — ESPN's live vocabulary is wider than
    /// its terminal one (STATUS_IN_PROGRESS, STATUS_HALFTIME,
    /// STATUS_END_PERIOD, STATUS_DELAYED, ...), and a status we've never
    /// seen (or a null from a missing CompetitionStatus row) must fail
    /// open into the stitch rather than blank a live card.
    /// </summary>
    private static readonly HashSet<string> NoSnapStateStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "STATUS_SCHEDULED",
        "STATUS_FINAL",
        "STATUS_POSTPONED",
        "STATUS_CANCELED",
        "STATUS_FORFEIT",
    };

    internal static Guid[] FilterLiveEligibleContestIds(IReadOnlyCollection<LeagueMatchupDto> matchups) =>
        matchups
            .Where(m => m.Status is null || !NoSnapStateStatuses.Contains(m.Status))
            .Select(m => m.ContestId)
            .Distinct()
            .ToArray();

    /// <summary>
    /// Football snap state (down, distance, ball spot) for the live card,
    /// read from the MOST RECENT PLAY — the same source the per-play
    /// SignalR events publish from, so a REST-populated card and a
    /// SignalR-populated one agree by construction.
    ///
    /// Deliberately NOT from CompetitionSituation. That row is created once
    /// per competition and never updated (its processor returns early when
    /// the row already exists), so it is frozen at the game's first snap —
    /// every situation row in the corpus has a null ModifiedUtc, and live
    /// games show "1st & 10" with the opening kickoff for the full sixty
    /// minutes.
    ///
    /// Football-gated because the snap columns are table-per-hierarchy:
    /// EndDown / EndDistance / EndYardLine are created by the football
    /// migrations and do not exist in the baseball database, so this cannot
    /// live in the shared SQL. Baseball has no equivalent per-play count
    /// state (its plays carry only Outs), so MLB gets the sport-neutral
    /// fields — period, clock, last play, batting side — and nothing here.
    /// </summary>
    internal async Task<Dictionary<Guid, LiveSituation>> GetLiveSituationsAsync(
        Guid[] contestIds,
        CancellationToken cancellationToken)
    {
        if (_dbContext is not FootballDataContext footballCtx)
        {
            return new Dictionary<Guid, LiveSituation>();
        }

        // Latest play per CONTEST. A Contest can host multiple Competitions
        // (doubleheaders / reschedule artifacts); ordering by SequenceNumber
        // then CreatedUtc makes the pick deterministic rather than relying
        // on whatever order Postgres returns.
        var rows = await footballCtx.Set<FootballCompetitionPlay>()
            .AsNoTracking()
            .Where(p => contestIds.Contains(p.Competition.ContestId))
            .Select(p => new
            {
                p.Competition.ContestId,
                p.SequenceNumber,
                p.CreatedUtc,
                p.StartDown,
                p.StartDistance,
                p.EndDown,
                p.EndDistance,
                p.EndYardLine,
                p.StartYardLine,
                p.StartFranchiseSeasonId,
                p.EndFranchiseSeasonId
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ContestId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    // SequenceNumber is stored as TEXT and is variable width
                    // (1–9 digits), so a string sort would put "9" above
                    // "100000" and pick an early play as the latest. Parsed
                    // with the SAME all-digits rule the SQL lateral uses, so
                    // both paths select the same play; anything else sorts
                    // last rather than hijacking the pick.
                    var latest = g
                        .OrderByDescending(r => ParseSequenceNumber(r.SequenceNumber) ?? long.MinValue)
                        .ThenByDescending(r => r.CreatedUtc)
                        .First();

                    // Down and distance travel as a PAIR: the END pair is
                    // used only when BOTH halves are present, otherwise the
                    // complete START pair wins. A half-populated end state
                    // ("2nd" with no distance) would otherwise beat a
                    // complete "3rd & 4" and describe a snap that never
                    // existed. Down 0 with distance 0 is a legitimate
                    // complete pair — ESPN's no-snap state — and is
                    // preserved by this rule, then nulled below.
                    var hasEndSnapState = latest.EndDown is not null && latest.EndDistance is not null;
                    var down = hasEndSnapState ? latest.EndDown : latest.StartDown;
                    var distance = hasEndSnapState ? latest.EndDistance : latest.StartDistance;

                    return new LiveSituation
                    {
                        // Down 0 is ESPN's no-snap-state value (kickoff,
                        // extra point, end of period) — surface it as null
                        // so the client renders no down-and-distance rather
                        // than "0th & 0".
                        Down = down > 0 ? down : null,
                        Distance = down > 0 ? distance : null,
                        BallOnYardLine = latest.EndYardLine ?? latest.StartYardLine,
                        // Who has the ball for the NEXT snap. End differs
                        // from Start on 1 in 6 plays — every punt, turnover
                        // and change of possession — so reading Start would
                        // credit the punting team with the ball.
                        PossessionFranchiseSeasonId =
                            latest.EndFranchiseSeasonId ?? latest.StartFranchiseSeasonId
                    };
                });
    }

    /// <summary>
    /// Accepts an ESPN play ordinal only when it is entirely digits and at
    /// most 18 of them — the same rule as the SQL lateral's ordering, so
    /// the two paths can never disagree about which play is latest. Null
    /// means "not orderable" and sorts last.
    ///
    /// The 18-digit bound comes from the SQL side, where ::bigint on a
    /// longer all-digits value raises "value out of range" and would fail
    /// the whole query; 18 nines always fits. Matching it here keeps the
    /// two rules identical rather than merely similar.
    /// </summary>
    private const int MaxOrderableSequenceDigits = 18;

    private static long? ParseSequenceNumber(string? sequenceNumber)
    {
        if (string.IsNullOrEmpty(sequenceNumber)) return null;
        if (sequenceNumber.Length > MaxOrderableSequenceDigits) return null;

        foreach (var c in sequenceNumber)
        {
            if (!char.IsAsciiDigit(c)) return null;
        }

        return long.TryParse(sequenceNumber, out var value) ? value : null;
    }

    /// <summary>Carrier for the football snap-state stitch.</summary>
    internal sealed class LiveSituation
    {
        public int? Down { get; init; }
        public int? Distance { get; init; }
        public int? BallOnYardLine { get; init; }
        public Guid? PossessionFranchiseSeasonId { get; init; }
    }

    /// <summary>
    /// Stitches MLB CurrentSeriesSummary onto the matchup result. Sport-gated:
    /// only runs against BaseballDataContext (NFL/NCAAFB no-op without a round
    /// trip). Mirrors the 2-phase stitch in <see cref="GetProbablePitchersAsync"/>.
    /// A Contest can host multiple Competitions (doubleheaders); the snapshot is
    /// locked-at-game-start per-Competition so any non-null value is acceptable
    /// — first match wins per ContestId.
    /// </summary>
    internal async Task<Dictionary<Guid, string>> GetCurrentSeriesSummariesAsync(
        Guid[] contestIds,
        CancellationToken cancellationToken)
    {
        if (_dbContext is not BaseballDataContext baseballCtx)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await baseballCtx.Competitions
            .AsNoTracking()
            .Where(c => contestIds.Contains(c.ContestId))
            .Where(c => c.CurrentSeriesSummary != null)
            .Select(c => new
            {
                c.ContestId,
                c.CurrentSeriesSummary
            })
            .ToListAsync(cancellationToken);

        var dict = new Dictionary<Guid, string>();
        foreach (var r in rows)
        {
            if (!dict.ContainsKey(r.ContestId) && !string.IsNullOrWhiteSpace(r.CurrentSeriesSummary))
            {
                dict[r.ContestId] = r.CurrentSeriesSummary!;
            }
        }
        return dict;
    }

    /// <summary>
    /// Stitches MLB probable starting pitchers onto the matchup result.
    /// Sport-gated: only runs when the underlying context is the Baseball
    /// one. NFL/NCAAFB Producer instances no-op without a round-trip,
    /// keeping the canonical matchups SQL sport-agnostic. Mirrors the
    /// 2-phase pattern in GetContestOverviewQueryHandler for headshots.
    /// </summary>
    internal async Task<Dictionary<Guid, (ProbablePitcherDto? Home, ProbablePitcherDto? Away)>> GetProbablePitchersAsync(
        Guid[] contestIds,
        CancellationToken cancellationToken)
    {
        var empty = new Dictionary<Guid, (ProbablePitcherDto?, ProbablePitcherDto?)>();

        if (_dbContext is not BaseballDataContext baseballCtx)
        {
            return empty;
        }

        const string ProbableStartingPitcherRole = "probableStartingPitcher";

        // Order by CompetitionCompetitorId for deterministic picking.
        // The unique index (CompetitionCompetitorId, Name) guarantees one
        // SP probable per CompetitionCompetitor — but a single Contest can
        // host multiple Competitions (1:N), so a Contest could in theory
        // surface multiple home/away SP rows. With this ordering combined
        // with the "first wins" stitch below, the chosen row is stable
        // across calls regardless of physical row order from Postgres.
        //
        // Two-phase headshot load: a flat projection here (no nested
        // OrderBy inside Select) followed by a separate Images query and
        // priority selection in C# memory. The previous one-phase form
        // with the priority OrderBy inlined in the Select was either
        // translated to SQL that ignored the priority or silently fell
        // back to insertion order — sportdeets-mark rows didn't win even
        // though the LINQ looked right.
        var rows = await baseballCtx.CompetitionCompetitorProbables
            .AsNoTracking()
            .Where(p => p.Name == ProbableStartingPitcherRole)
            .Where(p => contestIds.Contains(p.CompetitionCompetitor.Competition.ContestId))
            .OrderBy(p => p.CompetitionCompetitorId)
            .Select(p => new
            {
                ContestId = p.CompetitionCompetitor.Competition.ContestId,
                p.CompetitionCompetitor.HomeAway,
                p.AthleteSeason.DisplayName,
                AthleteId = (Guid?)p.AthleteSeason.AthleteId
            })
            .ToListAsync(cancellationToken);

        // Phase 2: fetch images for just the athletes we need, do the
        // priority selection (sportdeets-mark first, CreatedUtc tiebreak)
        // in C#. Translation surface is now trivial — Where + Select with
        // no nested ordering.
        var athleteIds = rows
            .Where(r => r.AthleteId.HasValue)
            .Select(r => r.AthleteId!.Value)
            .Distinct()
            .ToList();

        var headshotByAthleteId = new Dictionary<Guid, string>();
        if (athleteIds.Count > 0)
        {
            var imgRows = await baseballCtx.AthleteImages
                .AsNoTracking()
                .Where(i => athleteIds.Contains(i.AthleteId))
                .Select(i => new { i.AthleteId, i.Uri, i.Rel, i.CreatedUtc })
                .ToListAsync(cancellationToken);

            foreach (var grp in imgRows.GroupBy(i => i.AthleteId))
            {
                var winner = grp
                    .OrderBy(i => i.Rel != null && i.Rel.Contains("sportdeets-mark") ? 0 : 1)
                    .ThenBy(i => i.CreatedUtc)
                    .First();
                headshotByAthleteId[grp.Key] = winner.Uri.ToString();
            }
        }

        var dict = new Dictionary<Guid, (ProbablePitcherDto? Home, ProbablePitcherDto? Away)>();
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.DisplayName))
            {
                continue;
            }

            dict.TryGetValue(r.ContestId, out var entry);
            string? headshotUrl = null;
            if (r.AthleteId.HasValue)
            {
                headshotByAthleteId.TryGetValue(r.AthleteId.Value, out headshotUrl);
            }
            var pitcher = new ProbablePitcherDto
            {
                DisplayName = r.DisplayName!,
                HeadshotUrl = headshotUrl
            };

            // First match wins per side. Combined with the OrderBy above,
            // this means the lowest CompetitionCompetitorId is the chosen
            // pitcher when multiple Competitions share a Contest.
            if (string.Equals(r.HomeAway, "home", StringComparison.OrdinalIgnoreCase) && entry.Home is null)
            {
                entry = (pitcher, entry.Away);
            }
            else if (string.Equals(r.HomeAway, "away", StringComparison.OrdinalIgnoreCase) && entry.Away is null)
            {
                entry = (entry.Home, pitcher);
            }
            dict[r.ContestId] = entry;
        }
        return dict;
    }

    /// <summary>
    /// Resolves ScheduledTimeUtc for any actionable CompetitionStream rows
    /// (Scheduled / AwaitingStart / Active) whose competition belongs to the
    /// requested contests. Throws if a single ContestId resolves to multiple
    /// active rows — that violates the one-active-stream-per-contest invariant
    /// and we want a loud failure rather than silent picking.
    /// </summary>
    private async Task<Dictionary<Guid, DateTime>> GetActiveStreamTimesAsync(
        Guid[] contestIds,
        CancellationToken cancellationToken)
    {
        var activeStatuses = new[]
        {
            CompetitionStreamStatus.Scheduled,
            CompetitionStreamStatus.AwaitingStart,
            CompetitionStreamStatus.Active,
        };

        var rows = await _dbContext.CompetitionStreams
            .AsNoTracking()
            .Where(s => activeStatuses.Contains(s.Status))
            .Join(_dbContext.Competitions,
                s => s.CompetitionId,
                c => c.Id,
                (s, c) => new { c.ContestId, s.ScheduledTimeUtc })
            .Where(x => contestIds.Contains(x.ContestId))
            .ToListAsync(cancellationToken);

        var duplicates = rows
            .GroupBy(x => x.ContestId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            _logger.LogError(
                "Multiple active CompetitionStream rows resolved to the same ContestId(s): {ContestIds}. Violates one-active-stream-per-contest invariant.",
                duplicates);
            throw new InvalidOperationException(
                $"Multiple active CompetitionStream rows for ContestId(s): {string.Join(", ", duplicates)}");
        }

        return rows.ToDictionary(x => x.ContestId, x => x.ScheduledTimeUtc);
    }
}
