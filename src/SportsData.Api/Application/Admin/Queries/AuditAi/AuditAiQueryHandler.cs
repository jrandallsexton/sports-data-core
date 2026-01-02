using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.AuditAi;

public interface IAuditAiQueryHandler
{
    Task<Result<Guid>> ExecuteAsync(AuditAiQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Audits MatchupPreviews whose prediction was correct based on the narrative,
/// but the model hallucinated FranchiseSeasonId resulting in an incorrect pick
/// and the wrong scoring for accuracy
/// </summary>
public class AuditAiQueryHandler : IAuditAiQueryHandler
{
    private readonly ILogger<AuditAiQueryHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IProvideCanonicalData _canonicalData;

    public AuditAiQueryHandler(
        ILogger<AuditAiQueryHandler> logger,
        AppDataContext dataContext,
        IProvideCanonicalData canonicalData)
    {
        _logger = logger;
        _dataContext = dataContext;
        _canonicalData = canonicalData;
    }

    public async Task<Result<Guid>> ExecuteAsync(AuditAiQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            // Load only previews that have associated group matchups (server-side filtering)
            var previews = await _dataContext.MatchupPreviews
                .Where(mp => _dataContext.PickemGroupMatchups.Any(pgm => pgm.ContestId == mp.ContestId))
                .ToListAsync(cancellationToken);

            // Batch load all matchups in a single query
            var contestIds = previews.Select(p => p.ContestId).ToList();
            var matchupsByContestId = await _canonicalData.GetMatchupsForPreview(contestIds);

            var errorCount = 0;

            foreach (var preview in previews)
            {
                // Look up matchup from dictionary
                if (!matchupsByContestId.TryGetValue(preview.ContestId, out var matchup))
                {
                    _logger.LogError("Matchup not found for previewId {previewId}", preview.Id);
                    errorCount++;
                    continue;
                }

                if (preview.PredictedStraightUpWinner != matchup.AwayFranchiseSeasonId &&
                    preview.PredictedStraightUpWinner != matchup.HomeFranchiseSeasonId)
                {
                    // AI hallucinated the winning franchiseSeasonId
                    _logger.LogError("AI hallucinated the winning franchiseSeasonId for {previewId}", preview.Id);
                    errorCount++;
                }

                if (matchup.HomeSpread.HasValue)
                {
                    if (!preview.PredictedSpreadWinner.HasValue)
                    {
                        _logger.LogError("Matchup had a spread but AI did not generate one for previewId {previewId}", preview.Id);
                        errorCount++;
                        continue;
                    }

                    if (preview.PredictedSpreadWinner != matchup.AwayFranchiseSeasonId &&
                        preview.PredictedSpreadWinner != matchup.HomeFranchiseSeasonId)
                    {
                        // AI hallucinated the FranchiseSeasonId of the spread winner
                        _logger.LogError("AI hallucinated the spread winning franchiseSeasonId for {previewId}", preview.Id);
                        errorCount++;
                    }
                }
            }

            _logger.LogError($"!!! {errorCount} of {previews.Count} AI previews have issues with FranchiseSeasonId !!!");

            return new Success<Guid>(query.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to audit AI previews");
            return new Failure<Guid>(
                default,
                ResultStatus.Error,
                new List<ValidationFailure>
                {
                    new ValidationFailure("AuditAi.Failed", "An error occurred while auditing AI previews")
                });
        }
    }
}
