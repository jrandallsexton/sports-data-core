using Dapper;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsById;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestPreviewHistory;

public interface IGetContestPreviewHistoryQueryHandler
{
    Task<Result<ContestPreviewHistoryDto>> ExecuteAsync(
        GetContestPreviewHistoryQuery query,
        CancellationToken cancellationToken = default);
}

public class GetContestPreviewHistoryQueryHandler : IGetContestPreviewHistoryQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;
    private readonly IValidator<GetContestPreviewHistoryQuery> _validator;
    private readonly IGetFranchiseSeasonMetricsByIdQueryHandler _metricsHandler;

    public GetContestPreviewHistoryQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider,
        IValidator<GetContestPreviewHistoryQuery> validator,
        IGetFranchiseSeasonMetricsByIdQueryHandler metricsHandler)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
        _validator = validator;
        _metricsHandler = metricsHandler;
    }

    /// <summary>
    /// Dapper row for the prior-season query: a PreviewGameResultDto plus
    /// which TARGET team (Away/Home) the row belongs to.
    /// </summary>
    private class PriorSeasonRow : PreviewGameResultDto
    {
        public string Side { get; set; } = default!;
    }

    public async Task<Result<ContestPreviewHistoryDto>> ExecuteAsync(
        GetContestPreviewHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            return new Failure<ContestPreviewHistoryDto>(
                default!,
                ResultStatus.Validation,
                validationResult.Errors);
        }

        var connection = _dbContext.Database.GetDbConnection();

        var headToHead = (await connection.QueryAsync<PreviewGameResultDto>(
            new CommandDefinition(
                _sqlProvider.GetContestHeadToHeadResults(),
                new { query.ContestId, Count = query.MeetingCount },
                cancellationToken: cancellationToken))).ToList();

        var priorSeasonRows = (await connection.QueryAsync<PriorSeasonRow>(
            new CommandDefinition(
                _sqlProvider.GetContestPriorSeasonResults(),
                new { query.ContestId, Count = query.RecentGameCount },
                cancellationToken: cancellationToken))).ToList();

        // An unknown contest yields empty lists everywhere (the target CTE
        // matches nothing) — an empty history is a normal state for a
        // first-ever meeting, so no NotFound here; the caller degrades
        // gracefully either way.
        var dto = new ContestPreviewHistoryDto
        {
            HeadToHead = headToHead,
            AwayPriorSeasonGames = priorSeasonRows
                .Where(x => x.Side == "Away").Cast<PreviewGameResultDto>().ToList(),
            HomePriorSeasonGames = priorSeasonRows
                .Where(x => x.Side == "Home").Cast<PreviewGameResultDto>().ToList()
        };

        var target = await _dbContext.Contests
            .AsNoTracking()
            .Where(c => c.Id == query.ContestId)
            .Select(c => new
            {
                c.SeasonYear,
                c.AwayTeamFranchiseSeasonId,
                c.HomeTeamFranchiseSeasonId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (target is not null)
        {
            dto.AwayPriorSeason = await BuildPriorSeasonSummaryAsync(
                target.AwayTeamFranchiseSeasonId, target.SeasonYear, cancellationToken);
            dto.HomePriorSeason = await BuildPriorSeasonSummaryAsync(
                target.HomeTeamFranchiseSeasonId, target.SeasonYear, cancellationToken);
        }

        return new Success<ContestPreviewHistoryDto>(dto);
    }

    /// <summary>
    /// Prior-season summary for one team: resolve the franchise's
    /// SeasonYear-1 FranchiseSeason (cross-season identity via Franchise),
    /// take its final record, and attach that season's metrics when they
    /// exist. Null when the franchise has no prior season row.
    /// </summary>
    private async Task<PreviewPriorSeasonSummaryDto?> BuildPriorSeasonSummaryAsync(
        Guid currentFranchiseSeasonId,
        int targetSeasonYear,
        CancellationToken cancellationToken)
    {
        var priorSeason = await _dbContext.FranchiseSeasons
            .AsNoTracking()
            .Where(current => current.Id == currentFranchiseSeasonId)
            .SelectMany(current => _dbContext.FranchiseSeasons
                .Where(prior => prior.FranchiseId == current.FranchiseId
                             && prior.SeasonYear == targetSeasonYear - 1))
            .Select(prior => new
            {
                prior.Id,
                prior.SeasonYear,
                prior.Wins,
                prior.Losses,
                prior.ConferenceWins,
                prior.ConferenceLosses
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (priorSeason is null)
            return null;

        var metricsResult = await _metricsHandler.ExecuteAsync(
            new GetFranchiseSeasonMetricsByIdQuery(priorSeason.Id), cancellationToken);

        return new PreviewPriorSeasonSummaryDto
        {
            SeasonYear = priorSeason.SeasonYear,
            Wins = priorSeason.Wins,
            Losses = priorSeason.Losses,
            ConferenceWins = priorSeason.ConferenceWins,
            ConferenceLosses = priorSeason.ConferenceLosses,
            // NotFound = metrics simply not generated for that season — the
            // record still flows; the API's both-or-nothing rule handles
            // asymmetry before the model sees anything.
            Metrics = metricsResult.IsSuccess ? metricsResult.Value : null
        };
    }
}
