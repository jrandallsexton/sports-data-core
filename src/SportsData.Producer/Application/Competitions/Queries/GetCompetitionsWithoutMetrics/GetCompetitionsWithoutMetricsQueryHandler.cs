using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutMetrics;

public interface IGetCompetitionsWithoutMetricsQueryHandler
{
    Task<Result<List<CompetitionWithoutMetricsDto>>> ExecuteAsync(
        GetCompetitionsWithoutMetricsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>Moved from API's embedded CompetitionsWithoutMetrics.sql.</summary>
public class GetCompetitionsWithoutMetricsQueryHandler : IGetCompetitionsWithoutMetricsQueryHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly ICompetitionDiagnosticsScope _scope;

    public GetCompetitionsWithoutMetricsQueryHandler(TeamSportDataContext dataContext, ICompetitionDiagnosticsScope scope)
    {
        _dataContext = dataContext;
        _scope = scope;
    }

    public async Task<Result<List<CompetitionWithoutMetricsDto>>> ExecuteAsync(
        GetCompetitionsWithoutMetricsQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _scope.GetStartedCompetitionsAsync(_dataContext, query.SeasonYear, cancellationToken);
        if (scope is not Success<IQueryable<CompetitionBase>> competitions)
        {
            var failure = (Failure<IQueryable<CompetitionBase>>)scope;
            return new Failure<List<CompetitionWithoutMetricsDto>>([], failure.Status, failure.Errors);
        }

        var rows = await competitions.Value
            .Where(c => !c.Metrics.Any())
            .OrderBy(c => c.Contest.StartDateUtc)
            .Select(c => new CompetitionWithoutMetricsDto
            {
                Sport = c.Contest.Sport,
                ContestId = c.ContestId,
                ContestName = c.Contest.Name,
                StartDateUtc = c.Contest.StartDateUtc,
                CompetitionId = c.Id,
                MetricCount = 0
            })
            .ToListAsync(cancellationToken);

        return new Success<List<CompetitionWithoutMetricsDto>>(rows);
    }
}
