using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Matchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Previews;
using SportsData.Core.Infrastructure.Clients.AI;

using System.Text.Json;

namespace SportsData.Api.Application.Processors
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
            var matchup = await _canonicalDataProvider.GetMatchupForPreview(command.ContestId);

            var basePrompt = _promptProvider.PromptTemplate;
            var jsonInput = JsonSerializer.Serialize(matchup);

            const int maxAttempts = 5;
            MatchupPreviewResponse? parsed = null;
            string? rawResponse = null;
            List<string>? validationErrors = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var feedbackSection = validationErrors is { Count: > 0 }
                    ? $"\n\nNote: The last attempt produced invalid results for the following reasons:\n- {string.Join("\n- ", validationErrors)}"
                    : string.Empty;

                var fullPrompt = $"{basePrompt}\n\n{jsonInput}{feedbackSection}";

                rawResponse = await _aiCommunication.GetResponseAsync(fullPrompt, CancellationToken.None);

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    _logger.LogWarning("Attempt {Attempt} returned empty response from AI.", attempt);
                    continue;
                }

                try
                {
                    parsed = JsonSerializer.Deserialize<MatchupPreviewResponse>(rawResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed is null)
                    {
                        _logger.LogWarning("Attempt {Attempt} produced null after deserialization. Raw response: {Raw}", attempt, rawResponse);
                        continue;
                    }

                    if (matchup.HomeSpread.HasValue)
                    {
                        // Run semantic validation
                        var validation = MatchupPreviewValidator.Validate(
                            homeScore: parsed.HomeScore,
                            awayScore: parsed.AwayScore,
                            homeSpread: matchup.HomeSpread.Value,
                            predictedStraightUpWinner: parsed.PredictedStraightUpWinner,
                            predictedSpreadWinner: parsed.PredictedSpreadWinner,
                            homeFranchiseSeasonId: matchup.HomeFranchiseSeasonId,
                            awayFranchiseSeasonId: matchup.AwayFranchiseSeasonId
                        );

                        validationErrors = validation.IsValid ? null : validation.Errors;

                        if (validation.IsValid)
                            break; // We're good to proceed

                        _logger.LogWarning("Validation failed on attempt {Attempt}. Errors: {Errors}", attempt, validation.Errors);
                    }

                    break;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Attempt {Attempt} failed to deserialize AI response. Raw: {Raw}", attempt, rawResponse);
                }
            }

            // If parsing or validation never succeeded
            if (parsed == null || validationErrors is { Count: > 0 })
            {
                _logger.LogError("Failed to generate valid preview after {MaxAttempts} attempts. Last response: {Raw}", maxAttempts, rawResponse);
                return;
            }

            // We have a valid response (parsed + valid)
            _logger.LogInformation("AI generated preview. {@Parsed}", parsed);

            var preview = await _dataContext.MatchupPreviews
                .FirstOrDefaultAsync(x => x.ContestId == command.ContestId);

            if (preview == null)
            {
                await _dataContext.MatchupPreviews.AddAsync(new MatchupPreview
                {
                    Id = Guid.NewGuid(),
                    ContestId = command.ContestId,
                    Overview = parsed.Overview,
                    Analysis = parsed.Analysis,
                    Prediction = parsed.Prediction,
                    PredictedStraightUpWinner = parsed.PredictedStraightUpWinner,
                    PredictedSpreadWinner = parsed.PredictedSpreadWinner,
                    OverUnderPrediction = parsed.OverUnderPrediction == 1 ? OverUnderPrediction.Over : OverUnderPrediction.Under,
                    AwayScore = parsed.AwayScore,
                    HomeScore = parsed.HomeScore,
                    Model = _aiCommunication.GetModelName(),
                    ValidationErrors = null
                });
            }
            else
            {
                preview.Overview = parsed.Overview;
                preview.Analysis = parsed.Analysis;
                preview.Prediction = parsed.Prediction;
                preview.PredictedStraightUpWinner = parsed.PredictedStraightUpWinner;
                preview.PredictedSpreadWinner = parsed.PredictedSpreadWinner;
                preview.OverUnderPrediction = parsed.OverUnderPrediction == 1 ? OverUnderPrediction.Over : OverUnderPrediction.Under;
                preview.AwayScore = parsed.AwayScore;
                preview.HomeScore = parsed.HomeScore;
                preview.Model = _aiCommunication.GetModelName();
                preview.ValidationErrors = null;
            }

            await _eventBus.Publish(new PreviewGenerated(
                matchup.ContestId,
                $"{matchup.Home} @ {matchup.Away} preview generated",
                command.CorrelationId,
                Guid.NewGuid()));

            await _dataContext.SaveChangesAsync();
        }
    }
}
