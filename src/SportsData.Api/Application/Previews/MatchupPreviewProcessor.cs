using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews.Models;
using SportsData.Api.Application.UI.Matchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Previews;
using SportsData.Core.Infrastructure.Clients.AI;

using System.Text.Json;

namespace SportsData.Api.Application.Previews
{
    public class MatchupPreviewProcessor : IGenerateMatchupPreviews
    {
        private readonly AppDataContext _dataContext;
        private readonly ILogger<MatchupPreviewProcessor> _logger;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly IProvideAiCommunication _aiCommunication;
        private readonly MatchupPreviewPromptProvider _promptProvider;
        private readonly IEventBus _eventBus;

        public MatchupPreviewProcessor(
            AppDataContext dataContext,
            ILogger<MatchupPreviewProcessor> logger,
            IProvideCanonicalData canonicalDataProvider,
            IProvideAiCommunication aiCommunication,
            MatchupPreviewPromptProvider promptProvider,
            IEventBus eventBus)
        {
            _dataContext = dataContext;
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _aiCommunication = aiCommunication;
            _promptProvider = promptProvider;
            _eventBus = eventBus;
        }

        public async Task Process(GenerateMatchupPreviewsCommand command)
        {
            var rejectedPreview = await _dataContext.MatchupPreviews
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync(x => x.ContestId == command.ContestId && x.RejectedUtc != null);

            var matchup = await _canonicalDataProvider.GetMatchupForPreview(command.ContestId);

            matchup.AwayStats = await _canonicalDataProvider
                .GetFranchiseSeasonStatsForPreview(matchup.AwayFranchiseSeasonId);
            matchup.HomeStats = await _canonicalDataProvider
                .GetFranchiseSeasonStatsForPreview(matchup.HomeFranchiseSeasonId);

            matchup.AwayMetrics = await _canonicalDataProvider
                .GetFranchiseSeasonMetrics(matchup.AwayFranchiseSeasonId);
            matchup.HomeMetrics = await _canonicalDataProvider
                .GetFranchiseSeasonMetrics(matchup.HomeFranchiseSeasonId);

            if (matchup.AwayMetrics is null || matchup.HomeMetrics is null)
            {
                // Both or nothing
                matchup.AwayMetrics = null;
                matchup.HomeMetrics = null;
            }

            matchup.AwayCompetitionResults = await _canonicalDataProvider
                .GetFranchiseSeasonCompetitionResultsByFranchiseSeasonId(matchup.AwayFranchiseSeasonId);
            matchup.HomeCompetitionResults = await _canonicalDataProvider
                .GetFranchiseSeasonCompetitionResultsByFranchiseSeasonId(matchup.HomeFranchiseSeasonId);

            var hasStats = (matchup.AwayStats.RushingYardsPerGame.HasValue &&
                            matchup.HomeStats.RushingYardsPerGame.HasValue);

            var promptData = await _promptProvider.GetPreviewInsightPromptAsync(hasStats);

            var jsonInput = JsonSerializer.Serialize(matchup);

            const int maxAttempts = 5;
            MatchupPreviewResponse? parsed = null;
            string? rawResponse = null;
            List<string>? validationErrors = null;

            int attempt;

            for (attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var editorNote = attempt == 1 && rejectedPreview?.RejectionNote != null
                    ? $"\n\nAdditional feedback from the editor:\n\"{rejectedPreview.RejectionNote}\""
                    : string.Empty;

                var feedbackSection = validationErrors is { Count: > 0 }
                    ? $"\n\nNote: Your previous attempt produced invalid results for the following reasons:\n- {string.Join("\n- ", validationErrors)}"
                    : string.Empty;

                feedbackSection += editorNote;

                var fullPrompt = $"{promptData.PromptText}\n\n{jsonInput}{feedbackSection}";

                rawResponse = await _aiCommunication.GetResponseAsync(fullPrompt, CancellationToken.None);

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    _logger.LogError("Attempt {Attempt} returned empty response from AI.", attempt);
                    continue;
                }

                try
                {
                    parsed = JsonSerializer.Deserialize<MatchupPreviewResponse>(rawResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException primaryEx)
                {
                    _logger.LogWarning(primaryEx, "Flat deserialization failed on attempt {Attempt}. Trying V2 fallback...", attempt);

                    try
                    {
                        var fallback = JsonSerializer.Deserialize<MatchupPreviewResponseV2>(rawResponse, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        parsed = fallback?.ToV1();
                    }
                    catch (JsonException fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "V2 fallback deserialization also failed on attempt {Attempt}. Raw: {Raw}", attempt, rawResponse);
                        validationErrors = [
                            "Previous response was not valid JSON. Please ensure your response is valid JSON as described in the rules."
                        ];
                        continue;
                    }
                }

                // Exit if we still failed to parse anything
                if (parsed is null)
                {
                    _logger.LogError("Attempt {Attempt} produced null after both deserialization strategies. Raw response: {Raw}", attempt, rawResponse);
                    continue;
                }

                // Run semantic validation
                var validation = MatchupPreviewValidator.Validate(
                    contestId: command.ContestId,
                    homeScore: parsed.HomeScore,
                    awayScore: parsed.AwayScore,
                    homeSpread: matchup.HomeSpread ?? 0,
                    predictedStraightUpWinner: parsed.PredictedStraightUpWinner,
                    predictedSpreadWinner: parsed.PredictedSpreadWinner,
                    homeFranchiseSeasonId: matchup.HomeFranchiseSeasonId,
                    awayFranchiseSeasonId: matchup.AwayFranchiseSeasonId
                );

                validationErrors = validation.IsValid ? null : validation.Errors;

                if (validation.IsValid)
                    break;

                _logger.LogError("Validation failed on attempt {Attempt}. Errors: {Errors}", attempt, validation.Errors);

            }

            // If parsing or validation never succeeded
            if (parsed == null || validationErrors is { Count: > 0 })
            {
                _logger.LogError("Failed to generate valid preview after {MaxAttempts} attempts. Last response: {Raw}", maxAttempts, rawResponse);
                return;
            }

            // We have a valid response (parsed + valid)
            _logger.LogDebug("AI generated preview. {@Parsed}", parsed);

            var preview = new MatchupPreview
            {
                Id = Guid.NewGuid(),
                ContestId = command.ContestId,
                Overview = parsed.Overview,
                Analysis = parsed.Analysis,
                Prediction = parsed.Prediction,
                PredictedStraightUpWinner = parsed.PredictedStraightUpWinner,
                PredictedSpreadWinner = parsed.PredictedSpreadWinner,
                OverUnderPrediction = parsed.OverUnderPrediction == 1
                    ? OverUnderPrediction.Over
                    : OverUnderPrediction.Under,
                AwayScore = parsed.AwayScore,
                HomeScore = parsed.HomeScore,
                Model = _aiCommunication.GetModelName(),
                ValidationErrors = null,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = command.CorrelationId,
                PromptVersion = promptData.PromptName,
                IterationsRequired = attempt,
                UsedMetrics = matchup.AwayMetrics != null
            };

            await _dataContext.MatchupPreviews.AddAsync(preview);

            await _eventBus.Publish(new PreviewGenerated(
                matchup.ContestId,
                $"{matchup.Home} @ {matchup.Away} preview generated",
                command.CorrelationId,
                Guid.NewGuid()));

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Preview generated for {contestId}", preview.ContestId);
        }
    }
}
