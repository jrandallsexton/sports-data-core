using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Infrastructure.Clients.AI;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Processors
{
    public interface IGenerateMatchupPreviews
    {
        Task Process(GenerateMatchupPreviewsCommand command);
    }

    public class GenerateMatchupPreviewsCommand
    {
        public Guid ContestId { get; set; }
    }

    public class MatchupPreviewProcessor : IGenerateMatchupPreviews
    {
        private readonly AppDataContext _dataContext;
        private readonly ILogger<MatchupPreviewProcessor> _logger;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly IProvideAiCommunication _aiCommunication;
        private readonly MatchupPreviewPromptProvider _promptProvider;

        public MatchupPreviewProcessor(
            AppDataContext dataContext,
            ILogger<MatchupPreviewProcessor> logger,
            IProvideCanonicalData canonicalDataProvider,
            IProvideAiCommunication aiCommunication,
            MatchupPreviewPromptProvider promptProvider)
        {
            _dataContext = dataContext;
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _aiCommunication = aiCommunication;
            _promptProvider = promptProvider;
        }

        public async Task Process(GenerateMatchupPreviewsCommand command)
        {
            var previewExists = await _dataContext.MatchupPreviews.AnyAsync(x => x.ContestId == command.ContestId);
            if (previewExists)
            {
                _logger.LogInformation("Preview already exists. Skipping");
                return;
            }

            var matchup = await _canonicalDataProvider.GetMatchupForPreview(command.ContestId);

            var basePrompt = _promptProvider.PromptTemplate;
            var jsonInput = JsonSerializer.Serialize(matchup);
            var fullPrompt = $"{basePrompt}\n\n{jsonInput}";

            const int maxAttempts = 3;
            MatchupPreviewResponse? parsed = null;
            string? rawResponse = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
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

                    if (parsed is not null)
                        break;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Attempt {Attempt} failed to deserialize AI response. {@Response}", attempt, rawResponse);
                }
            }

            if (parsed == null)
            {
                _logger.LogError("Failed to deserialize a valid MatchupPreviewResponse after {MaxAttempts} attempts. Last raw response: {Raw}", maxAttempts, rawResponse);
                return;
            }

            _logger.LogInformation("AI generated the following preview. {@Preview}", parsed);

            var preview = await _dataContext.MatchupPreviews
                .FirstOrDefaultAsync(x => x.Id == command.ContestId);

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
                    OverUnderPrediction = parsed.OverUnderPrediction == "1" ? OverUnderPrediction.Over : OverUnderPrediction.Under,
                    AwayScore = parsed.AwayScore,
                    HomeScore = parsed.HomeScore,
                    Model = _aiCommunication.GetModelName()
                });
            }
            else
            {
                preview.Overview = parsed.Overview;
                preview.Analysis = parsed.Analysis;
                preview.Prediction = parsed.Prediction;
                preview.PredictedStraightUpWinner = parsed.PredictedStraightUpWinner;
                preview.PredictedSpreadWinner = parsed.PredictedSpreadWinner;
                preview.OverUnderPrediction = parsed.OverUnderPrediction == "1"
                    ? OverUnderPrediction.Over
                    : OverUnderPrediction.Under;
                preview.AwayScore = parsed.AwayScore;
                preview.HomeScore = parsed.HomeScore;
                preview.Model = _aiCommunication.GetModelName();
            }

            await _dataContext.SaveChangesAsync();
        }


        public class MatchupPreviewResponse
        {
            public required string Overview { get; set; }

            public required string Analysis { get; set; }

            public required string Prediction { get; set; }

            public Guid PredictedStraightUpWinner { get; set; }

            public Guid PredictedSpreadWinner { get; set; }

            public required string OverUnderPrediction { get; set; }

            public int AwayScore { get; set; }

            public int HomeScore { get; set; }
        }
    }
}
