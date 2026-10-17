using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutDrives;

public interface IGetCompetitionsWithoutDrivesQueryHandler
{
    Task<Result<List<CompetitionWithoutDrivesDto>>> ExecuteAsync(
        GetCompetitionsWithoutDrivesQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Moved from API's embedded CompetitionsWithoutDrives.sql. Football only:
/// DriveId lives on FootballCompetitionPlay, so this depends on
/// FootballDataContext and is registered on football pods only.
/// </summary>
public class GetCompetitionsWithoutDrivesQueryHandler : IGetCompetitionsWithoutDrivesQueryHandler
{
    private readonly FootballDataContext _dataContext;
    private readonly ICompetitionDiagnosticsScope _scope;

    public GetCompetitionsWithoutDrivesQueryHandler(FootballDataContext dataContext, ICompetitionDiagnosticsScope scope)
    {
        _dataContext = dataContext;
        _scope = scope;
    }

    public async Task<Result<List<CompetitionWithoutDrivesDto>>> ExecuteAsync(
        GetCompetitionsWithoutDrivesQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _scope.GetStartedCompetitionsAsync(_dataContext, query.SeasonYear, cancellationToken);
        if (scope is not Success<IQueryable<CompetitionBase>> competitions)
        {
            var failure = (Failure<IQueryable<CompetitionBase>>)scope;
            return new Failure<List<CompetitionWithoutDrivesDto>>([], failure.Status, failure.Errors);
        }

        var rows = await competitions.Value
            .Select(c => new CompetitionWithoutDrivesDto
            {
                Sport = c.Contest.Sport,
                ContestId = c.ContestId,
                ContestName = c.Contest.Name,
                StartDateUtc = c.Contest.StartDateUtc,
                CompetitionId = c.Id,
                PlayCount = _dataContext.CompetitionPlays.Count(p => p.CompetitionId == c.Id),
                PlaysWithDriveId = _dataContext.CompetitionPlays.Count(p => p.CompetitionId == c.Id && p.DriveId != null),
                // MAX(Text), as in the SQL this replaces: a sample of the play text, not the final play.
                LastPlayText = _dataContext.CompetitionPlays
                    .Where(p => p.CompetitionId == c.Id)
                    .Max(p => p.Text)
            })
            .Where(x => x.PlayCount > 0 && x.PlaysWithDriveId == 0)
            .OrderBy(x => x.StartDateUtc)
            .ToListAsync(cancellationToken);

        return new Success<List<CompetitionWithoutDrivesDto>>(rows);
    }
}
