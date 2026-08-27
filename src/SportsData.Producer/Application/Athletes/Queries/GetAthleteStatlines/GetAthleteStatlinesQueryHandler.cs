using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteStatlines;

public interface IGetAthleteStatlinesQueryHandler
{
    Task<Result<List<AthleteStatlineDto>>> ExecuteAsync(
        GetAthleteStatlinesQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Flattens AthleteCompetitionStatistic (unique per athlete-season +
/// competition; kept live-fresh by the play-driven refresh debounce)
/// into <c>category.statName → value</c> maps, scoped to the box-score
/// categories the scoring matrix draws from. Contest resolves through
/// Competition (1:1 by design).
/// </summary>
public class GetAthleteStatlinesQueryHandler : IGetAthleteStatlinesQueryHandler
{
    /// <summary>Categories the scoring matrix draws from — everything else (kick returns, defense until the DEF slot) stays out of the payload.</summary>
    private static readonly string[] ScoringCategories =
        ["passing", "rushing", "receiving", "fumbles", "kicking"];

    private readonly TeamSportDataContext _dataContext;
    private readonly IValidator<GetAthleteStatlinesQuery> _validator;

    public GetAthleteStatlinesQueryHandler(
        TeamSportDataContext dataContext,
        IValidator<GetAthleteStatlinesQuery> validator)
    {
        _dataContext = dataContext;
        _validator = validator;
    }

    public async Task<Result<List<AthleteStatlineDto>>> ExecuteAsync(
        GetAthleteStatlinesQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<List<AthleteStatlineDto>>(
                default!, ResultStatus.Validation, validation.Errors);
        }

        var rows = await (
                from acs in _dataContext.AthleteCompetitionStatistics.AsNoTracking()
                join comp in _dataContext.Competitions.AsNoTracking()
                    on acs.CompetitionId equals comp.Id
                where query.ContestIds.Contains(comp.ContestId) &&
                      query.AthleteSeasonIds.Contains(acs.AthleteSeasonId)
                from cat in acs.Categories
                where ScoringCategories.Contains(cat.Name)
                from stat in cat.Stats
                where stat.Value != null
                select new
                {
                    acs.AthleteSeasonId,
                    comp.ContestId,
                    Category = cat.Name,
                    Stat = stat.Name,
                    Value = stat.Value!.Value,
                })
            .ToListAsync(cancellationToken);

        var statlines = rows
            .GroupBy(r => (r.AthleteSeasonId, r.ContestId))
            .Select(g => new AthleteStatlineDto
            {
                AthleteSeasonId = g.Key.AthleteSeasonId,
                ContestId = g.Key.ContestId,
                Stats = g.ToDictionary(r => $"{r.Category}.{r.Stat}", r => r.Value),
            })
            .ToList();

        return new Success<List<AthleteStatlineDto>>(statlines);
    }
}
