using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Matchups.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Matchups.Queries.GetMatchupPreview;

public interface IGetMatchupPreviewQueryHandler
{
    Task<Result<MatchupPreviewDto>> ExecuteAsync(
        GetMatchupPreviewQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupPreviewQueryHandler : IGetMatchupPreviewQueryHandler
{
    private readonly ILogger<GetMatchupPreviewQueryHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IContestClientFactory _contestClientFactory;

    public GetMatchupPreviewQueryHandler(
        ILogger<GetMatchupPreviewQueryHandler> logger,
        AppDataContext dbContext,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
        _dbContext = dbContext;
        _contestClientFactory = contestClientFactory;
    }

    public async Task<Result<MatchupPreviewDto>> ExecuteAsync(
        GetMatchupPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var preview = await _dbContext.MatchupPreviews
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(x => x.ContestId == query.ContestId && x.RejectedUtc == null, cancellationToken);

        if (preview is null)
        {
            _logger.LogWarning("Matchup preview not found for contest {ContestId}", query.ContestId);
            return new Failure<MatchupPreviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.ContestId), "Matchup preview not found")]);
        }

        // TODO: multi-sport
        var previewResult = await _contestClientFactory.Resolve(SportsData.Core.Common.Sport.FootballNcaa).GetMatchupForPreview(query.ContestId);
        var canonical = previewResult.IsSuccess ? previewResult.Value : null;

        if (canonical is null)
        {
            _logger.LogError("Canonical matchup data not found for contest {ContestId}", query.ContestId);
            return new Failure<MatchupPreviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.ContestId), "Canonical matchup data not found")]);
        }

        var suWinner = canonical.AwayFranchiseSeasonId == preview.PredictedStraightUpWinner
            ? canonical.Away
            : canonical.Home;

        var atsWinner = canonical.AwayFranchiseSeasonId == preview.PredictedSpreadWinner
            ? canonical.Away
            : canonical.Home;

        var implied = (canonical is { HomeSpread: not null, OverUnder: not null })
            ? VegasScoreHelper.CalculateImpliedScore(canonical.HomeSpread.Value, canonical.OverUnder.Value)
            : string.Empty;

        var result = new MatchupPreviewDto
        {
            Id = preview.Id,
            ContestId = preview.ContestId,
            Overview = preview.Overview,
            Analysis = preview.Analysis,
            Prediction = preview.Prediction,
            StraightUpWinner = suWinner,
            AtsWinner = atsWinner,
            AwayScore = preview.AwayScore,
            HomeScore = preview.HomeScore,
            VegasImpliedScore = implied,
            GeneratedUtc = preview.CreatedUtc
        };

        return new Success<MatchupPreviewDto>(result);
    }
}
