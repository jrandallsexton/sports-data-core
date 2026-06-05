using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Common;
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
        foreach (var matchup in matchups)
        {
            matchup.StreamScheduledTimeUtc = streamTimes.GetValueOrDefault(matchup.ContestId);

            if (probables.TryGetValue(matchup.ContestId, out var pair))
            {
                matchup.HomeProbablePitcher = pair.Home;
                matchup.AwayProbablePitcher = pair.Away;
            }

            matchup.CurrentSeriesSummary = seriesSummaries.GetValueOrDefault(matchup.ContestId);
        }

        return new Success<List<LeagueMatchupDto>>(matchups);
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
                HeadshotUrl = p.AthleteSeason.Athlete != null && p.AthleteSeason.Athlete.Images.Any()
                    ? p.AthleteSeason.Athlete.Images.OrderBy(i => i.CreatedUtc).First().Uri.ToString()
                    : null
            })
            .ToListAsync(cancellationToken);

        var dict = new Dictionary<Guid, (ProbablePitcherDto? Home, ProbablePitcherDto? Away)>();
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.DisplayName))
            {
                continue;
            }

            dict.TryGetValue(r.ContestId, out var entry);
            var pitcher = new ProbablePitcherDto
            {
                DisplayName = r.DisplayName!,
                HeadshotUrl = r.HeadshotUrl
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
