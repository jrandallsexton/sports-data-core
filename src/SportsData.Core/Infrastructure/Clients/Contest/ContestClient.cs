using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
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
}
