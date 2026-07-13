using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Contest;

public interface IProvideContests : IProvideHealthChecks
{
    Task<Result<GetSeasonContestsResponse>> GetSeasonContests(
        Guid franchiseId,
        int seasonYear,
        int? week = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<Result<GetContestByIdResponse>> GetContestById(
        Guid contestId,
        CancellationToken cancellationToken = default);

    Task<Result<ContestOverviewDto>> GetContestOverviewByContestId(Guid contestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Full play-by-play log for a contest. Companion to the overview
    /// endpoint, which trims plays to key/scoring only — this returns the
    /// complete list (up to ~500 rows for an MLB game). Backs the
    /// on-demand "Show all plays" expansion in the UI.
    /// </summary>
    Task<Result<PlayLogDto>> GetContestPlayLogByContestId(Guid contestId, CancellationToken cancellationToken = default);

    Task<Result<bool>> RefreshContest(Guid contestId, CancellationToken cancellationToken = default);

    Task<Result<bool>> RefreshContestMediaByContestId(Guid contestId, CancellationToken cancellationToken = default);

    Task<Result<bool>> FinalizeContestByContestId(Guid contestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous admin "re-run enrichment" call. Producer clears the
    /// derived/enriched fields on the Contest row and re-invokes the
    /// sport-specific enrichment processor inline before returning, so the
    /// caller doesn't need a separate "is it done?" poll. Returns the
    /// CorrelationId Producer logged the work under for tracing in Seq.
    ///
    /// NOT a re-source — CompetitionCompetitorScores are not touched.
    /// Manual recovery path for stuck WinnerFranchiseSeasonId /
    /// SpreadWinnerFranchiseSeasonId that the nightly audit hasn't caught.
    /// </summary>
    Task<Result<ReenrichContestResponse>> ReenrichContest(Guid contestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a replay of a stored contest's plays through the bus →
    /// SignalR pipeline. Producer enqueues the work and returns
    /// 202 Accepted; the bus then emits <c>ContestStatusChanged</c> once
    /// followed by a <c>FootballPlayCompleted</c> / <c>BaseballPlayCompleted</c>
    /// per stored play (sport-keyed by the running Producer pod).
    /// </summary>
    Task<Result<bool>> ReplayContest(Guid contestId, CancellationToken cancellationToken = default);

    // Matchup query endpoints (Phase 2)
    Task<Result<List<Matchup>>> GetMatchupsForCurrentWeek(CancellationToken ct = default);
    Task<Result<List<Matchup>>> GetMatchupsForSeasonWeek(int year, int week, CancellationToken ct = default);

    /// <summary>
    /// Distinct calendar dates (US Eastern) that have at least one scheduled game
    /// in the [from, to] window. Either bound may be null for an open-ended
    /// range. Backs the create-league blackout-date picker and the create-time
    /// zero-game guard.
    /// </summary>
    Task<Result<List<DateOnly>>> GetGameDates(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<Result<Matchup>> GetMatchupByContestId(Guid contestId, CancellationToken ct = default);
    Task<Result<List<LeagueMatchupDto>>> GetMatchupsByContestIds(List<Guid> contestIds, MarkDirection direction, CancellationToken ct = default);
    Task<Result<MatchupForPreviewDto>> GetMatchupForPreview(Guid contestId, CancellationToken ct = default);
    Task<Result<Dictionary<Guid, MatchupForPreviewDto>>> GetMatchupsForPreviewBatch(List<Guid> contestIds, CancellationToken ct = default);
    Task<Result<MatchupResult>> GetMatchupResult(Guid contestId, CancellationToken ct = default);
    Task<Result<List<ContestResultDto>>> GetContestResultsByContestIds(List<Guid> contestIds, CancellationToken ct = default);
    Task<Result<List<Guid>>> GetFinalizedContestIds(Guid seasonWeekId, CancellationToken ct = default);
    Task<Result<List<Guid>>> GetCompletedFbsContestIds(Guid seasonWeekId, CancellationToken ct = default);
}

public class ContestClient : ClientBase, IProvideContests
{
    private readonly ILogger<ContestClient> _logger;

    public ContestClient(
        ILogger<ContestClient> logger,
        HttpClient httpClient) :
        base(httpClient)
    {
        _logger = logger;
    }

    public async Task<Result<GetSeasonContestsResponse>> GetSeasonContests(
        Guid franchiseId, 
        int seasonYear, 
        int? week = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pageNumber, pageSize);
        if (paginationError is not null)
        {
            return new Failure<GetSeasonContestsResponse>(
                new GetSeasonContestsResponse([]),
                ResultStatus.BadRequest,
                [paginationError]);
        }

        var weekParam = week.HasValue ? $"&week={week.Value}" : string.Empty;
        var url = $"franchises/{franchiseId}/seasons/{seasonYear}/contests?pageNumber={pageNumber}&pageSize={pageSize}{weekParam}";

        return await GetAsync<GetSeasonContestsResponse, List<SeasonContestDto>>(
            url,
            contests => new GetSeasonContestsResponse(contests),
            new GetSeasonContestsResponse([]),
            "Response",
            ResultStatus.BadRequest,
            cancellationToken);
    }

    public async Task<Result<GetContestByIdResponse>> GetContestById(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        if (contestId == Guid.Empty)
        {
            return new Failure<GetContestByIdResponse>(
                new GetContestByIdResponse(default!),
                ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);
        }

        return await GetAsync<GetContestByIdResponse, SeasonContestDto>(
            $"contests/{contestId}",
            contest => new GetContestByIdResponse(contest),
            new GetContestByIdResponse(default!),
            "Contest",
            ResultStatus.NotFound,
            cancellationToken);
    }

    public async Task<Result<ContestOverviewDto>> GetContestOverviewByContestId(Guid contestId, CancellationToken cancellationToken = default)
    {
        if (contestId == Guid.Empty)
        {
            return new Failure<ContestOverviewDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);
        }

        return await GetAsync<ContestOverviewDto, ContestOverviewDto>(
            $"contests/{contestId}/overview",
            overview => overview,
            default!,
            "Contest overview",
            ResultStatus.NotFound,
            cancellationToken);
    }

    public async Task<Result<PlayLogDto>> GetContestPlayLogByContestId(Guid contestId, CancellationToken cancellationToken = default)
    {
        if (contestId == Guid.Empty)
        {
            return new Failure<PlayLogDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);
        }

        return await GetAsync<PlayLogDto, PlayLogDto>(
            $"contests/{contestId}/playlog",
            playLog => playLog,
            default!,
            "Contest play log",
            ResultStatus.NotFound,
            cancellationToken);
    }

    public async Task<Result<bool>> RefreshContest(Guid contestId, CancellationToken cancellationToken = default)
    {
        if (contestId == Guid.Empty)
        {
            return new Failure<bool>(
                false,
                ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);
        }

        return await PostWithResultAsync(
            $"contests/{contestId}/update",
            "RefreshContest",
            cancellationToken);
    }

    public async Task<Result<bool>> RefreshContestMediaByContestId(Guid contestId, CancellationToken cancellationToken = default)
    {
        if (contestId == Guid.Empty)
        {
            return new Failure<bool>(
                false,
                ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);
        }

        return await PostWithResultAsync(
            $"contests/{contestId}/media/refresh",
            "RefreshContestMedia",
            cancellationToken);
    }

    public async Task<Result<bool>> ReplayContest(Guid contestId, CancellationToken cancellationToken = default)
    {
        if (contestId == Guid.Empty)
        {
            return new Failure<bool>(
                false,
                ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);
        }

        return await PostWithResultAsync(
            $"contests/{contestId}/replay",
            "ReplayContest",
            cancellationToken);
    }

    public async Task<Result<bool>> FinalizeContestByContestId(Guid contestId, CancellationToken cancellationToken = default)
    {
        if (contestId == Guid.Empty)
        {
            return new Failure<bool>(
                false,
                ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);
        }

        return await PostWithResultAsync(
            $"contests/{contestId}/enrich",
            "FinalizeContest",
            cancellationToken);
    }

    public async Task<Result<ReenrichContestResponse>> ReenrichContest(Guid contestId, CancellationToken cancellationToken = default)
    {
        if (contestId == Guid.Empty)
        {
            return new Failure<ReenrichContestResponse>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);
        }

        // Bodyless POST that returns a typed JSON response. No shared helper
        // exists for this shape (Result<T> + bodyless), so inline the
        // pattern. Mirrors the bodyless PostWithResultAsync's correlation-
        // header stamping so Producer logs the same id we trace from in
        // Seq.
        const string operationName = "ReenrichContest";
        var url = $"contests/{contestId}/admin/reenrich";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation(
                "X-Correlation-Id",
                ActivityExtensions.GetCorrelationId().ToString());

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = responseContent.FromJson<ReenrichContestResponse>();
                if (body is null)
                {
                    return new Failure<ReenrichContestResponse>(
                        default!,
                        ResultStatus.Error,
                        [new ValidationFailure(operationName, $"{operationName} succeeded but response body did not deserialize")]);
                }
                return new Success<ReenrichContestResponse>(body);
            }

            // MapHttpStatusCode is private on ClientBase, so map the common
            // shapes inline here. NotFound is the only response code that
            // needs a distinct ResultStatus today (Producer 404s when the
            // contest doesn't exist); everything else falls through to
            // generic Error so the API surface stays consistent.
            var status = response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? ResultStatus.NotFound
                : ResultStatus.Error;
            return new Failure<ReenrichContestResponse>(
                default!,
                status,
                [new ValidationFailure(operationName, $"{operationName} failed with status {response.StatusCode}")]);
        }
        catch (HttpRequestException ex)
        {
            return new Failure<ReenrichContestResponse>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure(operationName, $"{operationName} failed: {ex.Message}")]);
        }
        catch (TaskCanceledException ex)
        {
            return new Failure<ReenrichContestResponse>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure(operationName, $"{operationName} timed out: {ex.Message}")]);
        }
    }

    // ========== Matchup query methods (Phase 2) ==========

    public async Task<Result<List<Matchup>>> GetMatchupsForCurrentWeek(CancellationToken ct = default)
    {
        return await GetAsync<List<Matchup>>(
            "contests/matchups/current-week",
            new List<Matchup>(), "Matchups for current week", ResultStatus.NotFound, ct);
    }

    public async Task<Result<List<Matchup>>> GetMatchupsForSeasonWeek(int year, int week, CancellationToken ct = default)
    {
        return await GetAsync<List<Matchup>>(
            $"contests/matchups/by-season-week?year={year}&week={week}",
            new List<Matchup>(), "Matchups for season week", ResultStatus.NotFound, ct);
    }

    public async Task<Result<List<DateOnly>>> GetGameDates(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (from.HasValue)
            query.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to.HasValue)
            query.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

        return await GetAsync<List<DateOnly>>(
            $"contests/game-dates{queryString}",
            new List<DateOnly>(), "Game dates", ResultStatus.NotFound, ct);
    }

    public async Task<Result<Matchup>> GetMatchupByContestId(Guid contestId, CancellationToken ct = default)
    {
        if (contestId == Guid.Empty)
            return new Failure<Matchup>(default!, ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);

        return await GetAsync<Matchup>(
            $"contests/{contestId}/matchup",
            default!, "Matchup", ResultStatus.NotFound, ct);
    }

    public async Task<Result<List<LeagueMatchupDto>>> GetMatchupsByContestIds(List<Guid> contestIds, MarkDirection direction, CancellationToken ct = default)
    {
        if (contestIds is null || contestIds.Count == 0)
            return new Success<List<LeagueMatchupDto>>(new List<LeagueMatchupDto>());

        var request = new GetMatchupsByContestIdsRequest(contestIds.ToArray(), direction);
        var result = await PostOrDefaultAsync<List<LeagueMatchupDto>, GetMatchupsByContestIdsRequest>(
            "contests/matchups/by-ids", request, new List<LeagueMatchupDto>(), ct);
        return new Success<List<LeagueMatchupDto>>(result);
    }

    public async Task<Result<MatchupForPreviewDto>> GetMatchupForPreview(Guid contestId, CancellationToken ct = default)
    {
        if (contestId == Guid.Empty)
            return new Failure<MatchupForPreviewDto>(default!, ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);

        return await GetAsync<MatchupForPreviewDto>(
            $"contests/{contestId}/matchup-preview",
            default!, "Matchup preview", ResultStatus.NotFound, ct);
    }

    public async Task<Result<Dictionary<Guid, MatchupForPreviewDto>>> GetMatchupsForPreviewBatch(List<Guid> contestIds, CancellationToken ct = default)
    {
        if (contestIds is null || contestIds.Count == 0)
            return new Success<Dictionary<Guid, MatchupForPreviewDto>>(new Dictionary<Guid, MatchupForPreviewDto>());

        var result = await PostOrDefaultAsync<Dictionary<Guid, MatchupForPreviewDto>, Guid[]>(
            "contests/matchups/previews", contestIds.ToArray(), new Dictionary<Guid, MatchupForPreviewDto>(), ct);
        return new Success<Dictionary<Guid, MatchupForPreviewDto>>(result);
    }

    public async Task<Result<MatchupResult>> GetMatchupResult(Guid contestId, CancellationToken ct = default)
    {
        if (contestId == Guid.Empty)
            return new Failure<MatchupResult>(default!, ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);

        return await GetAsync<MatchupResult>(
            $"contests/{contestId}/result",
            default!, "Matchup result", ResultStatus.NotFound, ct);
    }

    public async Task<Result<List<ContestResultDto>>> GetContestResultsByContestIds(List<Guid> contestIds, CancellationToken ct = default)
    {
        if (contestIds is null || contestIds.Count == 0)
            return new Success<List<ContestResultDto>>(new List<ContestResultDto>());

        var result = await PostOrDefaultAsync<List<ContestResultDto>, Guid[]>(
            "contests/results/by-ids", contestIds.ToArray(), new List<ContestResultDto>(), ct);
        return new Success<List<ContestResultDto>>(result);
    }

    public async Task<Result<List<Guid>>> GetFinalizedContestIds(Guid seasonWeekId, CancellationToken ct = default)
    {
        if (seasonWeekId == Guid.Empty)
            return new Failure<List<Guid>>(new List<Guid>(), ResultStatus.BadRequest,
                [new ValidationFailure("seasonWeekId", "Season week ID cannot be empty")]);

        return await GetAsync<List<Guid>>(
            $"contests/finalized?seasonWeekId={seasonWeekId}",
            new List<Guid>(), "Finalized contest IDs", ResultStatus.NotFound, ct);
    }

    public async Task<Result<List<Guid>>> GetCompletedFbsContestIds(Guid seasonWeekId, CancellationToken ct = default)
    {
        if (seasonWeekId == Guid.Empty)
            return new Failure<List<Guid>>(new List<Guid>(), ResultStatus.BadRequest,
                [new ValidationFailure("seasonWeekId", "Season week ID cannot be empty")]);

        return await GetAsync<List<Guid>>(
            $"contests/completed-fbs?seasonWeekId={seasonWeekId}",
            new List<Guid>(), "Completed FBS contest IDs", ResultStatus.NotFound, ct);
    }
}
