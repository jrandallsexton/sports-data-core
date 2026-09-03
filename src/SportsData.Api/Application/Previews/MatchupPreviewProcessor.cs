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
        private readonly IAiModelClientResolver _modelClientResolver;

        public MatchupPreviewProcessor(
            AppDataContext dataContext,
            ILogger<MatchupPreviewProcessor> logger,
            IContestClientFactory contestClientFactory,
            IFranchiseClientFactory franchiseClientFactory,
            IProvideAiCommunication aiCommunication,
            IMatchupPreviewPromptProvider promptProvider,
            IEventBus eventBus,
            IDateTimeProvider dateTimeProvider,
            IAiModelClientResolver modelClientResolver)
        {
            _dataContext = dataContext;
            _logger = logger;
            _contestClientFactory = contestClientFactory;
            _franchiseClientFactory = franchiseClientFactory;
            _aiCommunication = aiCommunication;
            _promptProvider = promptProvider;
            _eventBus = eventBus;
            _dateTimeProvider = dateTimeProvider;            _modelClientResolver = modelClientResolver;

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

            // Hard gate, before any DB or Producer work: prompts exist only
            // for the sports MatchupPreviewPolicy lists. Everything that can
            // enqueue this job (league handlers, the recurring generator,
            // admin paths) funnels through here, so an unsupported sport can
            // never reach the model regardless of which door it came in.
            if (!MatchupPreviewPolicy.SupportsSport(command.Sport))
            {
                _logger.LogInformation(
                    "Preview generation not supported for {Sport} — no prompts exist; skipping. ContestId={ContestId}",
                    command.Sport, command.ContestId);
                return;
            }

            // Model Consensus Lab: resolve the Model row FIRST — a missing
            // or inactive model must cost nothing (no Producer round trip,
            // no prompt assembly). Inactive means inactive, including for
            // fan-out jobs enqueued moments before an admin flipped the row.
            Model? labModel = null;
            if (command.Mode == PreviewGenerationMode.Experiment && command.ModelId is not null)
            {
                labModel = await _dataContext.Models
                    .AsNoTracking()
                    .Include(x => x.ModelProvider)
                    .FirstOrDefaultAsync(x => x.Id == command.ModelId.Value);

                if (labModel is null || !labModel.IsActive || labModel.ModelProvider?.IsActive != true)
                {
                    _logger.LogWarning(
                        "Experiment references missing or inactive model {ModelId}; skipping. ContestId={ContestId}",
                        command.ModelId, command.ContestId);
                    return;
                }

                // A route without a lab client (direct first-party clients
                // arrive with panel promotion) is a skip, not a crash —
                // throwing here would put Hangfire into a pointless retry loop.
                if (!_modelClientResolver.CanResolve(labModel.Gateway, labModel.ModelProvider.Kind))
                {
                    _logger.LogWarning(
                        "No lab evaluation client for gateway {Gateway} / provider kind {Kind} (model {ModelName}); skipping. ContestId={ContestId}",
                        labModel.Gateway, labModel.ModelProvider.Kind, labModel.Name, command.ContestId);
                    return;
                }
            }

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

            // Model Consensus Lab: an experiment naming a Model row runs
            // against THAT model (resolved by provider Kind: OpenRouter
            // audition, or a direct client once a panel seat is earned) and
            // records the measurements the lab scores on. Everything else
            // uses the default production client, unchanged.
            Result<string> aiResponse;
            AiEvaluationResult? evaluation = null;

            if (labModel is not null)
            {
                var evalClient = _modelClientResolver.Resolve(labModel);
                var evalResult = await evalClient.EvaluateAsync(assembled.FullPrompt, CancellationToken.None);

                if (evalResult.IsSuccess)
                {
                    evaluation = evalResult.Value;
                    aiResponse = new Success<string>(evaluation.Content);
                }
                else
                {
                    aiResponse = new Failure<string>(
                        string.Empty,
                        evalResult.Status,
                        ((Failure<AiEvaluationResult>)evalResult).Errors);
                }
            }
            else
            {
                aiResponse = await _aiCommunication.GetResponseAsync(assembled.FullPrompt, CancellationToken.None);
            }

            var rawResponse = aiResponse.Value;

            if (command.Mode == PreviewGenerationMode.Experiment)
            {
                // Sandboxed eval run: record everything — including failures —
                // on the capture row. NEVER writes a MatchupPreview (an
                // experimental row would shadow a prior season's real preview
                // on the picks page) and never publishes PreviewGenerated.
                capture.Model = labModel?.ApiModelId ?? _aiCommunication.GetModelName();
                capture.ModelId = labModel?.Id;
                capture.RawResponse = string.IsNullOrWhiteSpace(rawResponse) ? null : rawResponse;
                capture.PromptTokens = evaluation?.PromptTokens;
                capture.CompletionTokens = evaluation?.CompletionTokens;
                capture.LatencyMs = evaluation?.LatencyMs;

                var problems = new List<string>();

                // Truncation is a CONFIG problem, not a model problem —
                // name it so the matrix's error tooltip says what to fix
                // instead of blaming the parse that inevitably follows.
                var truncated = string.Equals(
                    evaluation?.FinishReason, "length", StringComparison.OrdinalIgnoreCase);
                if (truncated)
                {
                    problems.Add(
                        $"TRUNCATED at the max-tokens ceiling (finish_reason=length, completion={evaluation?.CompletionTokens}) — raise OpenRouterClientConfig MaxTokens; reasoning models spend thinking tokens against the same cap");
                }

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
                        // A truncated response's parse failure is already
                        // explained by the truncation problem above.
                        if (!truncated)
                        {
                            problems.Add("Response could not be parsed by either deserialization strategy");
                        }
                    }
                    else
                    {
                        // Persist the parsed picks even when validation
                        // flags problems — the matrix scores the PICK; the
                        // problems column records the caveat alongside it.
                        capture.PredictedStraightUpWinnerId = experimentParsed.PredictedStraightUpWinner;
                        capture.PredictedSpreadWinnerId = experimentParsed.PredictedSpreadWinner;

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
                var attempt1Errors = string.Join("; ", validation.Errors);
                capture.ResponseValidationErrors =
                    MatchupPreviewValidator.ComposeErrorSections(attempt1Errors, null);

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
                    // Lab instead of vanishing with the log line — including
                    // WHY the retry produced nothing (request failure vs blank
                    // vs unparsable), alongside the attempt-1 errors. The
                    // compose helper guarantees the retry section survives
                    // truncation even when attempt-1 filled the column.
                    var retryDiagnostic = !retryResponse.IsSuccess
                        ? retryResponse is Failure<string> retryFailure
                            ? $"AI request failed - {string.Join(", ", retryFailure.Errors.Select(x => x.ErrorMessage))}"
                            : "AI request failed"
                        : string.IsNullOrWhiteSpace(rawResponse)
                            ? "AI returned no content"
                            : "response could not be parsed by either deserialization strategy";
                    capture.ResponseValidationErrors =
                        MatchupPreviewValidator.ComposeErrorSections(attempt1Errors, retryDiagnostic);
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
                    capture.ResponseValidationErrors = MatchupPreviewValidator.ComposeErrorSections(
                        attempt1Errors, string.Join("; ", validation.Errors));
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

            // Registry provenance: the IsDefault Model row IS the production
            // model selection. Stamp its id only when it agrees with the
            // client actually wired — a mismatch means the flag and the DI
            // config have drifted, and a null stamp beats a false one.
            Guid? productionModelId = null;
            var wiredModelName = _aiCommunication.GetModelName();
            var defaultModel = await _dataContext.Models
                .AsNoTracking()
                .Where(m => m.IsDefault)
                .Select(m => new { m.Id, m.ApiModelId })
                .FirstOrDefaultAsync();
            if (defaultModel is null)
            {
                _logger.LogWarning(
                    "No IsDefault Model row in the registry — preview {ContestId} gets string-only model provenance ({Model})",
                    command.ContestId, wiredModelName);
            }
            else if (!string.Equals(defaultModel.ApiModelId, wiredModelName, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "IsDefault Model row ({DefaultApiModelId}) does not match the wired production client ({Model}) — stamping no ModelId on preview {ContestId}",
                    defaultModel.ApiModelId, wiredModelName, command.ContestId);
            }
            else
            {
                productionModelId = defaultModel.Id;
            }

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
                Model = wiredModelName,
                ModelId = productionModelId,
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
            capture.ModelId = productionModelId;
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

        /// <summary>
        /// Strips a markdown code fence (```json ... ``` or ``` ... ```)
        /// wrapping the payload. Many models fence JSON out of habit
        /// (Claude Haiku did on the lab's first multi-model run) — the
        /// answer inside is valid, and discarding it would be waste, not
        /// rigor. Anything short of a leading fence is returned untouched.
        /// </summary>
        private static string StripCodeFence(string raw)
        {
            var trimmed = raw.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal))
                return raw;

            var firstLineBreak = trimmed.IndexOf('\n');
            if (firstLineBreak < 0)
                return raw;

            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence <= firstLineBreak)
                return raw;

            return trimmed[(firstLineBreak + 1)..closingFence].Trim();
        }

        private MatchupPreviewResponse? TryParseResponse(string rawResponse)
        {
            rawResponse = StripCodeFence(rawResponse);

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
