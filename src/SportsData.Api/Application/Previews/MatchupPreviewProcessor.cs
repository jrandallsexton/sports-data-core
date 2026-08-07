using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews.Models;
using SportsData.Api.Application.UI.Matchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Previews;
using SportsData.Core.Infrastructure.Clients.AI;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;

using System.Text.Json;

namespace SportsData.Api.Application.Previews
{
    public class MatchupPreviewProcessor : IGenerateMatchupPreviews
    {
        private readonly AppDataContext _dataContext;
        private readonly ILogger<MatchupPreviewProcessor> _logger;
        private readonly IContestClientFactory _contestClientFactory;
        private readonly IFranchiseClientFactory _franchiseClientFactory;
        private readonly IProvideAiCommunication _aiCommunication;
        private readonly MatchupPreviewPromptProvider _promptProvider;
        private readonly IEventBus _eventBus;
        private readonly IDateTimeProvider _dateTimeProvider;

        public MatchupPreviewProcessor(
            AppDataContext dataContext,
            ILogger<MatchupPreviewProcessor> logger,
            IContestClientFactory contestClientFactory,
            IFranchiseClientFactory franchiseClientFactory,
            IProvideAiCommunication aiCommunication,
            MatchupPreviewPromptProvider promptProvider,
            IEventBus eventBus,
            IDateTimeProvider dateTimeProvider)
        {
            _dataContext = dataContext;
            _logger = logger;
            _contestClientFactory = contestClientFactory;
            _franchiseClientFactory = franchiseClientFactory;
            _aiCommunication = aiCommunication;
            _promptProvider = promptProvider;
            _eventBus = eventBus;
            _dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Everything the model call needs, assembled by the single code path
        /// shared by capture and generation — a capture is byte-identical to
        /// what generation would send, by construction.
        /// </summary>
        internal sealed record AssembledPrompt(
            MatchupForPreviewDto Matchup,
            string PromptName,
            string InstructionText,
            string PayloadJson,
            string? EditorNote,
            string FullPrompt);

        public async Task Process(GenerateMatchupPreviewsCommand command)
        {
            _logger.LogInformation(
                "Preview processing started. ContestId: {ContestId}, Sport: {Sport}, Mode: {Mode}, CorrelationId: {CorrelationId}",
                command.ContestId, command.Sport, command.Mode, command.CorrelationId);

            var rejectedPreview = await _dataContext.MatchupPreviews
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync(x => x.ContestId == command.ContestId && x.RejectedUtc != null);

            var previewResult = await _contestClientFactory.Resolve(command.Sport).GetMatchupForPreview(command.ContestId);
            var matchup = previewResult.IsSuccess ? previewResult.Value : null;

            if (matchup is null)
            {
                // Surface WHY — this is the API -> Producer hop, and "not
                // found" here usually means the wrong sport was requested or
                // Producer for that sport is unreachable.
                var failureDetail = previewResult is Failure<MatchupForPreviewDto> matchupFailure
                    ? $"{matchupFailure.Status}: {string.Join(", ", matchupFailure.Errors.Select(x => x.ErrorMessage))}"
                    : "no failure detail";
                _logger.LogWarning(
                    "Matchup could not be resolved for ContestId {ContestId} via {Sport} Producer ({FailureDetail}). Skipping.",
                    command.ContestId, command.Sport, failureDetail);
                return;
            }

            // Capture/Experiment modes allow completed contests: replaying
            // last season's games is the backtest case.
            if (command.Mode == PreviewGenerationMode.Generate && ContestStatusValues.IsCompleted(matchup.Status))
            {
                _logger.LogInformation("Skipping preview generation for completed contest {ContestId}. Status: {Status}", command.ContestId, matchup.Status);
                return;
            }

            var assembled = await AssemblePromptAsync(matchup, command.Sport, rejectedPreview?.RejectionNote);

            _logger.LogInformation(
                "Prompt assembled for {ContestId}. PromptVersion: {PromptVersion}, CharCount: {CharCount}, UsedMetrics: {UsedMetrics}, AwayResults: {AwayResults}, HomeResults: {HomeResults}",
                command.ContestId,
                assembled.PromptName,
                assembled.FullPrompt.Length,
                assembled.Matchup.AwayMetrics != null,
                assembled.Matchup.AwayCompetitionResults?.Count ?? 0,
                assembled.Matchup.HomeCompetitionResults?.Count ?? 0);

            var capture = BuildCapture(command, assembled);
            await _dataContext.MatchupPreviewPrompts.AddAsync(capture);

            if (command.Mode == PreviewGenerationMode.Capture)
            {
                await _eventBus.Publish(new PreviewPromptCaptured(
                    matchup.ContestId,
                    $"{matchup.Away} @ {matchup.Home} prompt captured ({capture.EstTokens} est. tokens)",
                    null,
                    matchup.Sport,
                    matchup.SeasonYear,
                    command.CorrelationId,
                    CausationId.Api.MatchupPreviewProcessor));

                await _dataContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Prompt captured for {ContestId} without model call. CaptureId: {CaptureId}, EstTokens: {EstTokens}",
                    matchup.ContestId, capture.Id, capture.EstTokens);
                return;
            }

            var aiResponse = await _aiCommunication.GetResponseAsync(assembled.FullPrompt, CancellationToken.None);
            var rawResponse = aiResponse.Value;

            if (command.Mode == PreviewGenerationMode.Experiment)
            {
                // Sandboxed eval run: record everything — including failures —
                // on the capture row. NEVER writes a MatchupPreview (an
                // experimental row would shadow a prior season's real preview
                // on the picks page) and never publishes PreviewGenerated.
                capture.Model = _aiCommunication.GetModelName();
                capture.RawResponse = string.IsNullOrWhiteSpace(rawResponse) ? null : rawResponse;

                var problems = new List<string>();

                if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(rawResponse))
                {
                    problems.Add(aiResponse is Failure<string> aiFailure
                        ? string.Join(", ", aiFailure.Errors.Select(x => x.ErrorMessage))
                        : "AI request returned no content");
                }
                else
                {
                    var experimentParsed = TryParseResponse(rawResponse);
                    if (experimentParsed is null)
                    {
                        problems.Add("Response could not be parsed by either deserialization strategy");
                    }
                    else
                    {
                        var experimentValidation = MatchupPreviewValidator.Validate(
                            contestId: command.ContestId,
                            homeScore: experimentParsed.HomeScore,
                            awayScore: experimentParsed.AwayScore,
                            homeSpread: assembled.Matchup.HomeSpread ?? 0,
                            predictedStraightUpWinner: experimentParsed.PredictedStraightUpWinner,
                            predictedSpreadWinner: experimentParsed.PredictedSpreadWinner,
                            homeFranchiseSeasonId: assembled.Matchup.HomeFranchiseSeasonId,
                            awayFranchiseSeasonId: assembled.Matchup.AwayFranchiseSeasonId);

                        if (!experimentValidation.IsValid)
                            problems.AddRange(experimentValidation.Errors);
                    }
                }

                capture.ResponseValidationErrors = problems.Count > 0 ? string.Join("; ", problems) : null;

                await _eventBus.Publish(new PreviewPromptCaptured(
                    matchup.ContestId,
                    $"{matchup.Away} @ {matchup.Home} experiment completed ({capture.Model}{(capture.ResponseValidationErrors is null ? "" : ", with validation errors")})",
                    null,
                    matchup.Sport,
                    matchup.SeasonYear,
                    command.CorrelationId,
                    CausationId.Api.MatchupPreviewProcessor));

                await _dataContext.SaveChangesAsync();

                _logger.LogInformation("Experiment completed for {contestId}", matchup.ContestId);
                return;
            }

            if (!aiResponse.IsSuccess || string.IsNullOrWhiteSpace(rawResponse))
            {
                var errorMsg = aiResponse is Failure<string> f
                    ? string.Join(", ", f.Errors.Select(x => x.ErrorMessage))
                    : "Unknown error";
                _logger.LogError("AI request failed. Error: {Error}", errorMsg);
                return;
            }

            var parsed = TryParseResponse(rawResponse);

            // Exit if we still failed to parse anything
            if (parsed is null)
            {
                _logger.LogError("Produced null after both deserialization strategies. Raw response: {Raw}", rawResponse);
                return;
            }

            // Run semantic validation
            var validation = MatchupPreviewValidator.Validate(
                contestId: command.ContestId,
                homeScore: parsed.HomeScore,
                awayScore: parsed.AwayScore,
                homeSpread: assembled.Matchup.HomeSpread ?? 0,
                predictedStraightUpWinner: parsed.PredictedStraightUpWinner,
                predictedSpreadWinner: parsed.PredictedSpreadWinner,
                homeFranchiseSeasonId: assembled.Matchup.HomeFranchiseSeasonId,
                awayFranchiseSeasonId: assembled.Matchup.AwayFranchiseSeasonId
            );

            if (!validation.IsValid)
            {
                _logger.LogError("Validation failed. Errors: {Errors}", validation.Errors);
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
                CreatedUtc = _dateTimeProvider.UtcNow(),
                CreatedBy = command.CorrelationId,
                PromptVersion = assembled.PromptName,
                IterationsRequired = 1,
                UsedMetrics = assembled.Matchup.AwayMetrics != null
            };

            await _dataContext.MatchupPreviews.AddAsync(preview);

            capture.MatchupPreviewId = preview.Id;
            capture.Model = preview.Model;
            capture.RawResponse = rawResponse;

            await _eventBus.Publish(new PreviewGenerated(
                assembled.Matchup.ContestId,
                $"{assembled.Matchup.Away} @ {assembled.Matchup.Home} preview generated",
                null,
                assembled.Matchup.Sport,
                assembled.Matchup.SeasonYear,
                command.CorrelationId,
                CausationId.Api.MatchupPreviewProcessor));

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Preview generated for {contestId}", preview.ContestId);
        }

        private async Task<AssembledPrompt> AssemblePromptAsync(
            MatchupForPreviewDto matchup,
            Sport sport,
            string? rejectionNote)
        {
            var franchiseClient = _franchiseClientFactory.Resolve(sport);

            matchup.AwayStats = await franchiseClient
                .GetFranchiseSeasonPreviewStats(matchup.AwayFranchiseSeasonId);
            matchup.HomeStats = await franchiseClient
                .GetFranchiseSeasonPreviewStats(matchup.HomeFranchiseSeasonId);

            matchup.AwayMetrics = await franchiseClient
                .GetFranchiseSeasonMetricsByFranchiseSeasonId(matchup.AwayFranchiseSeasonId);
            matchup.HomeMetrics = await franchiseClient
                .GetFranchiseSeasonMetricsByFranchiseSeasonId(matchup.HomeFranchiseSeasonId);

            if (matchup.AwayMetrics is null || matchup.HomeMetrics is null)
            {
                // Both or nothing
                matchup.AwayMetrics = null;
                matchup.HomeMetrics = null;
            }

            matchup.AwayCompetitionResults = await franchiseClient
                .GetFranchiseSeasonCompetitionResults(matchup.AwayFranchiseSeasonId);
            matchup.HomeCompetitionResults = await franchiseClient
                .GetFranchiseSeasonCompetitionResults(matchup.HomeFranchiseSeasonId);

            var hasStats = (matchup.AwayStats.RushingYardsPerGame.HasValue &&
                            matchup.HomeStats.RushingYardsPerGame.HasValue);

            var promptData = await _promptProvider.GetPreviewInsightPromptAsync(hasStats);

            var jsonInput = JsonSerializer.Serialize(matchup);

            var editorNote = rejectionNote != null
                ? $"\n\nAdditional feedback from the editor:\n\"{rejectionNote}\""
                : string.Empty;

            var fullPrompt = $"{promptData.PromptText}\n\n{jsonInput}{editorNote}";

            return new AssembledPrompt(
                matchup,
                promptData.PromptName,
                promptData.PromptText,
                jsonInput,
                string.IsNullOrEmpty(editorNote) ? null : rejectionNote,
                fullPrompt);
        }

        private MatchupPreviewPrompt BuildCapture(
            GenerateMatchupPreviewsCommand command,
            AssembledPrompt assembled)
        {
            return new MatchupPreviewPrompt
            {
                Id = Guid.NewGuid(),
                ContestId = command.ContestId,
                Sport = command.Sport,
                PromptVersion = assembled.PromptName,
                PromptText = assembled.InstructionText,
                PayloadJson = assembled.PayloadJson,
                EditorNote = assembled.EditorNote,
                CharCount = assembled.FullPrompt.Length,
                EstTokens = assembled.FullPrompt.Length / 4,
                Mode = command.Mode,
                CreatedUtc = _dateTimeProvider.UtcNow(),
                CreatedBy = command.CorrelationId
            };
        }

        private MatchupPreviewResponse? TryParseResponse(string rawResponse)
        {
            try
            {
                return JsonSerializer.Deserialize<MatchupPreviewResponse>(rawResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException primaryEx)
            {
                _logger.LogWarning(primaryEx, "Flat deserialization failed. Trying V2 fallback...");

                try
                {
                    var fallback = JsonSerializer.Deserialize<MatchupPreviewResponseV2>(rawResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return fallback?.ToV1();
                }
                catch (JsonException fallbackEx)
                {
                    _logger.LogError(fallbackEx, "V2 fallback deserialization also failed. Raw: {Raw}", rawResponse);
                    return null;
                }
            }
        }
    }
}
