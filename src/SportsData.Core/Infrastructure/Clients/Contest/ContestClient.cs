using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
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

    Task<Result<bool>> RefreshContest(Guid contestId, CancellationToken cancellationToken = default);

    Task<Result<bool>> RefreshContestMediaByContestId(Guid contestId, CancellationToken cancellationToken = default);

    Task<Result<bool>> FinalizeContestByContestId(Guid contestId, CancellationToken cancellationToken = default);

    // Matchup query endpoints (Phase 2)
    Task<Result<List<Matchup>>> GetMatchupsForCurrentWeek(CancellationToken ct = default);
    Task<Result<List<Matchup>>> GetMatchupsForSeasonWeek(int year, int week, CancellationToken ct = default);
    Task<Result<Matchup>> GetMatchupByContestId(Guid contestId, CancellationToken ct = default);
    Task<Result<List<LeagueMatchupDto>>> GetMatchupsByContestIds(List<Guid> contestIds, CancellationToken ct = default);
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

    public async Task<Result<Matchup>> GetMatchupByContestId(Guid contestId, CancellationToken ct = default)
    {
        if (contestId == Guid.Empty)
            return new Failure<Matchup>(default!, ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);

        return await GetAsync<Matchup>(
            $"contests/{contestId}/matchup",
            default!, "Matchup", ResultStatus.NotFound, ct);
    }

    public async Task<Result<List<LeagueMatchupDto>>> GetMatchupsByContestIds(List<Guid> contestIds, CancellationToken ct = default)
    {
        if (contestIds is null || contestIds.Count == 0)
            return new Success<List<LeagueMatchupDto>>(new List<LeagueMatchupDto>());

        var result = await PostOrDefaultAsync<List<LeagueMatchupDto>, Guid[]>(
            "contests/matchups/by-ids", contestIds.ToArray(), new List<LeagueMatchupDto>(), ct);
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
