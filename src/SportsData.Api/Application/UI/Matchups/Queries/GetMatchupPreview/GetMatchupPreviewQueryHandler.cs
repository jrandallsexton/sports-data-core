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

        // Derive the sport server-side from the contest's league — every
        // previewed contest is in a PickemGroup by construction (previews
        // are generated off group events / league weeks). Falls back to
        // NCAA for any orphan so legacy behavior is preserved.
        var sport = await _dbContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.ContestId == query.ContestId)
            .Join(
                _dbContext.PickemGroups,
                m => m.GroupId,
                g => g.Id,
                (m, g) => (Sport?)g.Sport)
            .FirstOrDefaultAsync(cancellationToken) ?? Sport.FootballNcaa;

        var previewResult = await _contestClientFactory.Resolve(sport).GetMatchupForPreview(query.ContestId, cancellationToken);
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
            GeneratedUtc = preview.CreatedUtc,
            // Canonical status is authoritative — approve/reject is
            // meaningless once the game has been played, so clients hide
            // those admin affordances when this is true.
            IsContestCompleted = canonical.Status == "STATUS_FINAL"
        };

        return new Success<MatchupPreviewDto>(result);
    }
}
