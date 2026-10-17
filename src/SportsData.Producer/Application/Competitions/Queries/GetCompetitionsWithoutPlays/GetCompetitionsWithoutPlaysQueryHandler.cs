using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutPlays;

public interface IGetCompetitionsWithoutPlaysQueryHandler
{
    Task<Result<List<CompetitionWithoutPlaysDto>>> ExecuteAsync(
        GetCompetitionsWithoutPlaysQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>Moved from API's embedded CompetitionsWithoutPlays.sql, with the same threshold (10 plays or fewer).</summary>
public class GetCompetitionsWithoutPlaysQueryHandler : IGetCompetitionsWithoutPlaysQueryHandler
{
    /// <summary>A game with this many plays or fewer is treated as missing its play-by-play.</summary>
    public const int MaxPlayCount = 10;

    private readonly TeamSportDataContext _dataContext;
    private readonly ICompetitionDiagnosticsScope _scope;

    public GetCompetitionsWithoutPlaysQueryHandler(TeamSportDataContext dataContext, ICompetitionDiagnosticsScope scope)
    {
        _dataContext = dataContext;
        _scope = scope;
    }

    public async Task<Result<List<CompetitionWithoutPlaysDto>>> ExecuteAsync(
        GetCompetitionsWithoutPlaysQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _scope.GetStartedCompetitionsAsync(_dataContext, query.SeasonYear, cancellationToken);
        if (scope is not Success<IQueryable<CompetitionBase>> competitions)
        {
            var failure = (Failure<IQueryable<CompetitionBase>>)scope;
            return new Failure<List<CompetitionWithoutPlaysDto>>([], failure.Status, failure.Errors);
        }

        var rows = await competitions.Value
            .Select(c => new CompetitionWithoutPlaysDto
            {
                Sport = c.Contest.Sport,
                ContestId = c.ContestId,
                ContestName = c.Contest.Name,
                StartDateUtc = c.Contest.StartDateUtc,
                CompetitionId = c.Id,
                PlayCount = _dataContext.CompetitionPlays.Count(p => p.CompetitionId == c.Id),
                // MAX(Text), as in the SQL this replaces: a sample of the play
                // text, not the final play. SequenceNumber is an ESPN string
                // and does not order numerically.
                LastPlayText = _dataContext.CompetitionPlays
                    .Where(p => p.CompetitionId == c.Id)
                    .Max(p => p.Text)
            })
            .Where(x => x.PlayCount <= MaxPlayCount)
            .OrderBy(x => x.StartDateUtc)
            .ToListAsync(cancellationToken);

        return new Success<List<CompetitionWithoutPlaysDto>>(rows);
    }
}
