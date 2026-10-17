using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.Application.GroupSeasons.Queries.GetFbsGroupSeasonIds;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Competitions.Queries;

/// <summary>
/// The competitions a data-coverage diagnostic (plays, drives, metrics)
/// expects data for: this pod's sport, one season, already started and not
/// cancelled (a cancelled contest is terminal and never gets data). On the
/// NCAA pod, only games with at least one FBS team, the same rule as
/// RefreshAllCompetitionMediaCommandHandler; other sports are unfiltered.
/// Replaces API's embedded SQL, which hard-coded FBS 2025: it returned
/// nothing for NFL and only last season for NCAA.
/// </summary>
public interface ICompetitionDiagnosticsScope
{
    /// <param name="dataContext">
    /// The caller's context. The query composes on it, so a handler on
    /// FootballDataContext (drives) can extend it with football-only sets.
    /// </param>
    /// <param name="seasonYear">
    /// Null = the latest season with a started contest. An explicit season
    /// with no started contests is NotFound, not an empty (clean-looking)
    /// result: a typo'd year must not read as "no problems".
    /// </param>
    Task<Result<IQueryable<CompetitionBase>>> GetStartedCompetitionsAsync(
        TeamSportDataContext dataContext,
        int? seasonYear,
        CancellationToken cancellationToken);
}

public class CompetitionDiagnosticsScope : ICompetitionDiagnosticsScope
{
    private readonly IGetFbsGroupSeasonIdsQueryHandler _getFbsGroupSeasonIds;
    private readonly IAppMode _appMode;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CompetitionDiagnosticsScope(
        IGetFbsGroupSeasonIdsQueryHandler getFbsGroupSeasonIds,
        IAppMode appMode,
        IDateTimeProvider dateTimeProvider)
    {
        _getFbsGroupSeasonIds = getFbsGroupSeasonIds;
        _appMode = appMode;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IQueryable<CompetitionBase>>> GetStartedCompetitionsAsync(
        TeamSportDataContext dataContext,
        int? seasonYear,
        CancellationToken cancellationToken)
    {
        var sport = _appMode.CurrentSport;
        var now = _dateTimeProvider.UtcNow();

        var season = seasonYear ?? await dataContext.Contests
            .AsNoTracking()
            .Where(c => c.Sport == sport && c.StartDateUtc < now && c.CancelledUtc == null)
            .MaxAsync(c => (int?)c.SeasonYear, cancellationToken);

        if (seasonYear is not null)
        {
            var seasonStarted = await dataContext.Contests
                .AsNoTracking()
                .AnyAsync(c => c.Sport == sport && c.SeasonYear == seasonYear && c.StartDateUtc < now && c.CancelledUtc == null, cancellationToken);

            if (!seasonStarted)
            {
                return new Failure<IQueryable<CompetitionBase>>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(seasonYear), $"No started {sport} contests in season {seasonYear}.")]);
            }
        }

        var competitions = dataContext.Competitions
            .AsNoTracking()
            .Where(c => c.Contest.Sport == sport
                        && c.Contest.SeasonYear == season
                        && c.Contest.StartDateUtc < now
                        && c.Contest.CancelledUtc == null);

        if (season is null || sport != Sport.FootballNcaa)
        {
            return new Success<IQueryable<CompetitionBase>>(competitions);
        }

        var fbs = await _getFbsGroupSeasonIds.ExecuteAsync(new GetFbsGroupSeasonIdsQuery(season.Value), cancellationToken);
        if (fbs is not Success<HashSet<Guid>> fbsSuccess)
        {
            var failure = (Failure<HashSet<Guid>>)fbs;
            return new Failure<IQueryable<CompetitionBase>>(default!, failure.Status, failure.Errors);
        }

        var fbsGroupIds = fbsSuccess.Value;

        return new Success<IQueryable<CompetitionBase>>(competitions.Where(c =>
            (c.Contest.AwayTeamFranchiseSeason.GroupSeasonId.HasValue
             && fbsGroupIds.Contains(c.Contest.AwayTeamFranchiseSeason.GroupSeasonId.Value))
            || (c.Contest.HomeTeamFranchiseSeason.GroupSeasonId.HasValue
                && fbsGroupIds.Contains(c.Contest.HomeTeamFranchiseSeason.GroupSeasonId.Value))));
    }
}
