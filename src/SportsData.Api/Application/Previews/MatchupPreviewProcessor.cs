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
        private readonly IMatchupPreviewPromptProvider _promptProvider;
        private readonly IEventBus _eventBus;
        private readonly IDateTimeProvider _dateTimeProvider;

        public MatchupPreviewProcessor(
            AppDataContext dataContext,
            ILogger<MatchupPreviewProcessor> logger,
            IContestClientFactory contestClientFactory,
            IFranchiseClientFactory franchiseClientFactory,
            IProvideAiCommunication aiCommunication,
            IMatchupPreviewPromptProvider promptProvider,
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
            Guid PromptId,
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

            if (command.PromptId is not null && command.Mode == PreviewGenerationMode.Generate)
            {
                // Guard rail: an experiment prompt override must never leak
                // into production previews.
                _logger.LogWarning(
                    "PromptId {PromptId} ignored for Generate mode on {ContestId} — overrides are Capture/Experiment only.",
                    command.PromptId, command.ContestId);
            }

            var assembled = await AssemblePromptAsync(matchup, command.Sport, command.Mode, rejectedPreview?.RejectionNote,
                command.Mode == PreviewGenerationMode.Generate ? null : command.PromptId);

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

            MatchupPreviewValidator.ValidationResult Validate(MatchupPreviewResponse p) =>
                MatchupPreviewValidator.Validate(
                    contestId: command.ContestId,
                    homeScore: p.HomeScore,
                    awayScore: p.AwayScore,
                    homeSpread: assembled.Matchup.HomeSpread ?? 0,
                    predictedStraightUpWinner: p.PredictedStraightUpWinner,
                    predictedSpreadWinner: p.PredictedSpreadWinner,
                    homeFranchiseSeasonId: assembled.Matchup.HomeFranchiseSeasonId,
                    awayFranchiseSeasonId: assembled.Matchup.AwayFranchiseSeasonId);

            // Truncate to the capture column's 1024-char cap.
            static string ErrorText(List<string> errors)
            {
                var joined = string.Join("; ", errors);
                return joined.Length <= 1024 ? joined : joined[..1024];
            }

            var validation = Validate(parsed);
            var iterations = 1;

            if (!validation.IsValid)
            {
                // One bounded retry with the violations fed back — the
                // automated twin of the human editor-rejection loop. Without
                // it, a failed validation silently costs the game its preview
                // until the next scheduled run rolls fresh dice. First-attempt
                // errors stay on the capture either way, so the Lab shows the
                // recovery, and IterationsRequired records the extra call.
                _logger.LogWarning(
                    "Preview validation failed for {ContestId} (attempt 1); retrying with feedback. Errors: {Errors}",
                    command.ContestId, validation.Errors);
                capture.ResponseValidationErrors = ErrorText(validation.Errors);

                var retryPrompt =
                    $"{assembled.FullPrompt}\n\nYour previous response:\n{rawResponse}\n\n" +
                    $"It failed validation for these reasons:\n- {string.Join("\n- ", validation.Errors)}\n\n" +
                    "Generate a corrected response that resolves every issue and keeps all fields consistent with each other.";

                var retryResponse = await _aiCommunication.GetResponseAsync(retryPrompt, CancellationToken.None);
                rawResponse = retryResponse.Value;
                iterations = 2;

                parsed = !retryResponse.IsSuccess || string.IsNullOrWhiteSpace(rawResponse)
                    ? null
                    : TryParseResponse(rawResponse);

                if (parsed is null)
                {
                    // Persist the capture so the failure is auditable in the
                    // Lab instead of vanishing with the log line.
                    capture.Model = _aiCommunication.GetModelName();
                    capture.RawResponse = string.IsNullOrWhiteSpace(rawResponse) ? null : rawResponse;
                    await _dataContext.SaveChangesAsync();
                    _logger.LogError(
                        "Preview retry response unusable for {ContestId}; no preview written.",
                        command.ContestId);
                    return;
                }

                validation = Validate(parsed);
                if (!validation.IsValid)
                {
                    // Attempt-1 errors were recorded above; append the retry's.
                    capture.ResponseValidationErrors = ErrorText(
                        [capture.ResponseValidationErrors!, .. validation.Errors.Select(e => $"Retry: {e}")]);
                    capture.Model = _aiCommunication.GetModelName();
                    capture.RawResponse = rawResponse;
                    await _dataContext.SaveChangesAsync();
                    _logger.LogError(
                        "Preview validation failed after retry for {ContestId}; no preview written. Errors: {Errors}",
                        command.ContestId, validation.Errors);
                    return;
                }
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
                PromptId = assembled.PromptId,
                IterationsRequired = iterations,
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
            PreviewGenerationMode mode,
            string? rejectionNote,
            Guid? promptId = null)
        {
            // Historical context (last 5 H2H + prior-season last 5, preview-
            // safe semantics baked into Producer's queries). Degrades
            // gracefully: a failed fetch logs and the preview proceeds
            // without history rather than failing the job.
            var historyResult = await _contestClientFactory.Resolve(sport)
                .GetContestPreviewHistory(matchup.ContestId);

            if (historyResult.IsSuccess && historyResult.Value is not null)
            {
                matchup.HeadToHead = historyResult.Value.HeadToHead;
                matchup.AwayPriorSeasonGames = historyResult.Value.AwayPriorSeasonGames;
                matchup.HomePriorSeasonGames = historyResult.Value.HomePriorSeasonGames;
                matchup.AwayPriorSeason = historyResult.Value.AwayPriorSeason;
                matchup.HomePriorSeason = historyResult.Value.HomePriorSeason;

                // Spread-conditioned facts ("The Line"): pre-verified numbers
                // the narrative can cite — the model reads facts, never
                // computes them. GUID-free by construction, and as-of-capped
                // in Producer so capture/experiment runs on completed games
                // stay leak-free.
                matchup.SpreadContext = historyResult.Value.SpreadContext;

                // Same both-or-nothing rule as current-season metrics:
                // asymmetric analytics would bias the model toward the
                // covered team. Records keep flowing either way.
                if (matchup.AwayPriorSeason?.Metrics is null || matchup.HomePriorSeason?.Metrics is null)
                {
                    if (matchup.AwayPriorSeason is not null) matchup.AwayPriorSeason.Metrics = null;
                    if (matchup.HomePriorSeason is not null) matchup.HomePriorSeason.Metrics = null;
                }
            }
            else
            {
                _logger.LogWarning(
                    "Preview history unavailable for {ContestId}; proceeding without historical blocks.",
                    matchup.ContestId);
            }

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

            // As-of rule: the payload must never contain any game starting on
            // or after the target. For a completed target (capture/experiment
            // on a historical game) the raw CompetitionResults include the
            // TARGET ITSELF — final score, winner, spread winner: the answer.
            // For a pre-game target this is a no-op (those games haven't been
            // played), so applying it uniformly keeps captures byte-identical
            // to what real generation sends.
            matchup.AwayCompetitionResults = matchup.AwayCompetitionResults?
                .Where(r => r.StartDateUtc < matchup.StartDateUtc).ToList();
            matchup.HomeCompetitionResults = matchup.HomeCompetitionResults?
                .Where(r => r.StartDateUtc < matchup.StartDateUtc).ToList();

            // Status masking: a completed target's STATUS_FINAL/"Final" also
            // leaks that the game is over. Recreate the pre-game information
            // state for capture/experiment runs; Generate never reaches here
            // for completed contests, so live behavior is untouched.
            if (mode != PreviewGenerationMode.Generate && ContestStatusValues.IsCompleted(matchup.Status))
            {
                matchup.Status = ContestStatusValues.ScheduledRaw;
                matchup.StatusDescription = "Scheduled";
            }

            var hasStats = (matchup.AwayStats.RushingYardsPerGame.HasValue &&
                            matchup.HomeStats.RushingYardsPerGame.HasValue);

            var promptData = await _promptProvider.GetPromptAsync(
                new PreviewPromptRequest(sport, hasStats, promptId));

            var jsonInput = SerializePromptPayload(matchup);

            var editorNote = rejectionNote != null
                ? $"\n\nAdditional feedback from the editor:\n\"{rejectionNote}\""
                : string.Empty;

            var fullPrompt = $"{promptData.PromptText}\n\n{jsonInput}{editorNote}";

            return new AssembledPrompt(
                matchup,
                promptData.PromptId,
                promptData.PromptName,
                promptData.PromptText,
                jsonInput,
                string.IsNullOrEmpty(editorNote) ? null : rejectionNote,
                fullPrompt);
        }

        private static readonly JsonSerializerOptions PromptPayloadOptions = new()
        {
            // Hygiene projection (design doc §3.5/3e): every value in the
            // payload should be usable by the model verbatim.
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        /// <summary>
        /// The purpose-built prompt payload: the wire DTO serialized with
        /// omit-null + string enums, minus fields the model cannot use —
        /// ContestId (leaving exactly the two live FranchiseSeasonIds the
        /// output contract needs) and AwaySpread (spread is ALWAYS
        /// home-relative; the away value is a derived negation that invites
        /// sign confusion).
        /// </summary>
        private static string SerializePromptPayload(MatchupForPreviewDto matchup)
        {
            var node = JsonSerializer.SerializeToNode(matchup, PromptPayloadOptions)!.AsObject();
            node.Remove("ContestId");
            node.Remove("AwaySpread");

            // Legacy CompetitionResults rows carry five GUIDs each (contest,
            // both franchise-seasons, winner, spread-winner) — per-season ids
            // the model could echo into predictedStraightUpWinner — plus their
            // own derived AwaySpread. Slugs + scores + HomeSpread remain, from
            // which winner and cover are trivially derivable. After this walk
            // the ONLY GUIDs in the payload are the two live Away/Home
            // FranchiseSeasonIds the output contract requires.
            foreach (var listName in new[] { "AwayCompetitionResults", "HomeCompetitionResults" })
            {
                if (node[listName] is not System.Text.Json.Nodes.JsonArray rows) continue;
                foreach (var row in rows.OfType<System.Text.Json.Nodes.JsonObject>())
                {
                    row.Remove("ContestId");
                    row.Remove("AwayFranchiseSeasonId");
                    row.Remove("HomeFranchiseSeasonId");
                    row.Remove("WinnerFranchiseSeasonId");
                    row.Remove("SpreadWinnerFranchiseSeasonId");
                    row.Remove("AwaySpread");
                }
            }

            return node.ToJsonString();
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
                PromptId = assembled.PromptId,
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
