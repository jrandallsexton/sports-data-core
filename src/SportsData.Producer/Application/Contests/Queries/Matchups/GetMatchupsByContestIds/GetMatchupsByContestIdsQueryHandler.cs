using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Enums;
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

        var connection = _dbContext.Database.GetDbConnection();
        var matchups = (await connection.QueryAsync<LeagueMatchupDto>(
            new CommandDefinition(sql, new { ContestIds = query.ContestIds }, cancellationToken: cancellationToken)))
            .ToList();

        var streamTimes = await GetActiveStreamTimesAsync(query.ContestIds, cancellationToken);
        foreach (var matchup in matchups)
        {
            matchup.StreamScheduledTimeUtc = streamTimes.GetValueOrDefault(matchup.ContestId);
        }

        return new Success<List<LeagueMatchupDto>>(matchups);
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
