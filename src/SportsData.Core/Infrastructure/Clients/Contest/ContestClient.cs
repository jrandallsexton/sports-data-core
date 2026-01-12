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
        // Input validation
        if (pageNumber < 1)
        {
            return new Failure<GetSeasonContestsResponse>(
                new GetSeasonContestsResponse([]),
                ResultStatus.BadRequest,
                [new ValidationFailure("pageNumber", "Page number must be greater than or equal to 1")]);
        }

        if (pageSize < 1)
        {
            return new Failure<GetSeasonContestsResponse>(
                new GetSeasonContestsResponse([]),
                ResultStatus.BadRequest,
                [new ValidationFailure("pageSize", "Page size must be greater than or equal to 1")]);
        }

        if (pageSize > 500)
        {
            return new Failure<GetSeasonContestsResponse>(
                new GetSeasonContestsResponse([]),
                ResultStatus.BadRequest,
                [new ValidationFailure("pageSize", "Page size must not exceed 500")]);
        }

        var weekParam = week.HasValue ? $"&week={week.Value}" : string.Empty;
        var url = $"franchises/{franchiseId}/seasons/{seasonYear}/contests?pageNumber={pageNumber}&pageSize={pageSize}{weekParam}";
        
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var contests = content.FromJson<List<SeasonContestDto>>();

            if (contests == null)
            {
                return new Failure<GetSeasonContestsResponse>(
                    new GetSeasonContestsResponse([]),
                    ResultStatus.BadRequest,
                    [new ValidationFailure("Response", "Unable to deserialize contests response")]);
            }

            return new Success<GetSeasonContestsResponse>(new GetSeasonContestsResponse(contests));
        }

        var failure = content.FromJson<Failure<GetSeasonContestsResponse>>();

        var status = failure?.Status ?? ResultStatus.BadRequest;
        var errors = failure?.Errors ?? [new ValidationFailure("Response", "Unknown error deserializing contest data")];

        return new Failure<GetSeasonContestsResponse>(new GetSeasonContestsResponse([]), status, errors);
    }

    public async Task<Result<GetContestByIdResponse>> GetContestById(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        // Input validation
        if (contestId == Guid.Empty)
        {
            return new Failure<GetContestByIdResponse>(
                new GetContestByIdResponse(default!),
                ResultStatus.BadRequest,
                [new ValidationFailure("contestId", "Contest ID cannot be empty")]);
        }

        using var response = await HttpClient.GetAsync($"contests/{contestId}", cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var contest = content.FromJson<SeasonContestDto>();

            if (contest is null)
            {
                return new Failure<GetContestByIdResponse>(
                    new GetContestByIdResponse(default!),
                    ResultStatus.BadRequest,
                    [new ValidationFailure("Contest", $"Unable to deserialize contest with id {contestId}")]);
            }

            return new Success<GetContestByIdResponse>(new GetContestByIdResponse(contest));
        }

        var failure = content.FromJson<Failure<GetContestByIdResponse>>();

        var status = failure?.Status ?? ResultStatus.NotFound;
        var errors = failure?.Errors ?? [new ValidationFailure("Contest", $"Contest with id {contestId} not found")];

        return new Failure<GetContestByIdResponse>(new GetContestByIdResponse(default!), status, errors);
    }
}
