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

        var streamTimes = await GetActiveStreamTimesAsync(query.ContestIds, cancellationToken);
        var probables = await GetProbablePitchersAsync(query.ContestIds, cancellationToken);
        var seriesSummaries = await GetCurrentSeriesSummariesAsync(query.ContestIds, cancellationToken);
        var situations = await GetLiveSituationsAsync(query.ContestIds, cancellationToken);
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
                matchup.Balls = situation.Balls;
                matchup.Strikes = situation.Strikes;
                matchup.Outs = situation.Outs;
                matchup.RunnerOnFirst = situation.RunnerOnFirst;
                matchup.RunnerOnSecond = situation.RunnerOnSecond;
                matchup.RunnerOnThird = situation.RunnerOnThird;
            }
        }

        return new Success<List<LeagueMatchupDto>>(matchups);
    }

    /// <summary>
    /// Sport-specific live situation state, stitched onto the matchup result
    /// so a cold start mid-game renders a complete live card instead of
    /// waiting for the next SignalR play.
    ///
    /// This CANNOT live in the shared SQL: CompetitionSituation is a
    /// table-per-hierarchy table whose subtype columns are created by each
    /// sport's own migrations, so the football database has
    /// Down/Distance/YardLine and the baseball database has
    /// Balls/Strikes/Outs/runners — referencing either from the shared
    /// query would fail on the other sport's database. Dispatching on the
    /// concrete DbContext keeps each query against columns that exist.
    /// </summary>
    internal async Task<Dictionary<Guid, LiveSituation>> GetLiveSituationsAsync(
        Guid[] contestIds,
        CancellationToken cancellationToken)
    {
        // A Contest can host multiple Competitions (doubleheaders); the live
        // situation is per-Competition, so first match wins per ContestId —
        // the same convention as the probables / series stitches above.
        if (_dbContext is FootballDataContext footballCtx)
        {
            var rows = await footballCtx.Set<FootballCompetitionSituation>()
                .AsNoTracking()
                .Where(s => contestIds.Contains(s.Competition.ContestId))
                .Select(s => new
                {
                    s.Competition.ContestId,
                    s.Down,
                    s.Distance,
                    s.YardLine
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.ContestId)
                .ToDictionary(
                    g => g.Key,
                    g => new LiveSituation
                    {
                        // Down 0 is ESPN's no-snap-state value (kickoff,
                        // extra point, end of period) — surface it as null
                        // so the client renders no down-and-distance rather
                        // than "0th & 0".
                        Down = g.First().Down > 0 ? g.First().Down : null,
                        Distance = g.First().Down > 0 ? g.First().Distance : null,
                        BallOnYardLine = g.First().YardLine
                    });
        }

        if (_dbContext is BaseballDataContext baseballCtx)
        {
            var rows = await baseballCtx.Set<BaseballCompetitionSituation>()
                .AsNoTracking()
                .Where(s => contestIds.Contains(s.Competition.ContestId))
                .Select(s => new
                {
                    s.Competition.ContestId,
                    s.Balls,
                    s.Strikes,
                    s.Outs,
                    s.OnFirstAthleteSeasonId,
                    s.OnSecondAthleteSeasonId,
                    s.OnThirdAthleteSeasonId
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.ContestId)
                .ToDictionary(
                    g => g.Key,
                    g => new LiveSituation
                    {
                        Balls = g.First().Balls,
                        Strikes = g.First().Strikes,
                        Outs = g.First().Outs,
                        RunnerOnFirst = g.First().OnFirstAthleteSeasonId != null,
                        RunnerOnSecond = g.First().OnSecondAthleteSeasonId != null,
                        RunnerOnThird = g.First().OnThirdAthleteSeasonId != null
                    });
        }

        return new Dictionary<Guid, LiveSituation>();
    }

    /// <summary>Sport-agnostic carrier for the per-sport situation stitch.</summary>
    internal sealed class LiveSituation
    {
        public int? Down { get; init; }
        public int? Distance { get; init; }
        public int? BallOnYardLine { get; init; }
        public int? Balls { get; init; }
        public int? Strikes { get; init; }
        public int? Outs { get; init; }
        public bool? RunnerOnFirst { get; init; }
        public bool? RunnerOnSecond { get; init; }
        public bool? RunnerOnThird { get; init; }
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
