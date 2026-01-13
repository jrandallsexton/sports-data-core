using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Franchise.Queries;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Franchise;

public interface IProvideFranchises : IProvideHealthChecks
{
    Task<Result<GetFranchisesResponse>> GetFranchises(int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<Result<GetFranchiseByIdResponse>> GetFranchiseById(string id, CancellationToken cancellationToken = default);
    Task<Result<GetFranchiseSeasonsResponse>> GetFranchiseSeasons(Guid franchiseId, CancellationToken cancellationToken = default);
    Task<Result<GetFranchiseSeasonByIdResponse>> GetFranchiseSeasonById(Guid franchiseId, int seasonYear, CancellationToken cancellationToken = default);
    Task<List<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetrics(int seasonYear, CancellationToken cancellationToken = default);
    Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId, CancellationToken cancellationToken = default);
    Task<List<FranchiseSeasonPollDto>> GetFranchiseSeasonRankings(int seasonYear, CancellationToken cancellationToken = default);
}

public class FranchiseClient : ClientBase, IProvideFranchises
{
    private readonly ILogger<FranchiseClient> _logger;

    public FranchiseClient(
        ILogger<FranchiseClient> logger,
        HttpClient httpClient) :
        base(httpClient)
    {
        _logger = logger;
    }

    public async Task<Result<GetFranchisesResponse>> GetFranchises(int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pageNumber, pageSize);
        if (paginationError is not null)
        {
            return new Failure<GetFranchisesResponse>(
                new GetFranchisesResponse(),
                ResultStatus.BadRequest,
                [paginationError]);
        }

        return await GetAsync(
            $"franchises?pageNumber={pageNumber}&pageSize={pageSize}",
            new GetFranchisesResponse(),
            "Response",
            ResultStatus.BadRequest,
            cancellationToken);
    }

    public async Task<Result<GetFranchiseByIdResponse>> GetFranchiseById(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new Failure<GetFranchiseByIdResponse>(
                new GetFranchiseByIdResponse(null),
                ResultStatus.BadRequest,
                [new ValidationFailure("id", "Franchise ID cannot be null or empty")]);
        }

        return await GetAsync<GetFranchiseByIdResponse, FranchiseDto>(
            $"franchises/{id}",
            franchise => new GetFranchiseByIdResponse(franchise),
            new GetFranchiseByIdResponse(null),
            "Franchise",
            ResultStatus.NotFound,
            cancellationToken);
    }

    public async Task<Result<GetFranchiseSeasonsResponse>> GetFranchiseSeasons(Guid franchiseId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<GetFranchiseSeasonsResponse, List<FranchiseSeasonDto>>(
            $"franchises/{franchiseId}/seasons",
            seasons => new GetFranchiseSeasonsResponse { Seasons = seasons },
            new GetFranchiseSeasonsResponse(),
            "Response",
            ResultStatus.BadRequest,
            cancellationToken);
    }

    public async Task<Result<GetFranchiseSeasonByIdResponse>> GetFranchiseSeasonById(Guid franchiseId, int seasonYear, CancellationToken cancellationToken = default)
    {
        return await GetAsync<GetFranchiseSeasonByIdResponse, FranchiseSeasonDto>(
            $"franchises/{franchiseId}/seasons/{seasonYear}",
            season => new GetFranchiseSeasonByIdResponse(season),
            new GetFranchiseSeasonByIdResponse(null),
            "FranchiseSeason",
            ResultStatus.NotFound,
            cancellationToken);
    }

    public async Task<List<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetrics(int seasonYear, CancellationToken cancellationToken = default)
    {
        return await GetOrDefaultAsync(
            $"franchise-seasons/seasonYear/{seasonYear}/metrics",
            new List<FranchiseSeasonMetricsDto>(),
            cancellationToken);
    }

    public async Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId, CancellationToken cancellationToken = default)
    {
        return await GetOrDefaultAsync(
            $"franchise-seasons/id/{franchiseSeasonId}/metrics",
            new FranchiseSeasonMetricsDto(),
            cancellationToken);
    }

    public async Task<List<FranchiseSeasonPollDto>> GetFranchiseSeasonRankings(int seasonYear, CancellationToken cancellationToken = default)
    {
        return await GetOrDefaultAsync(
            $"franchise-season-rankings/seasonYear/{seasonYear}",
            new List<FranchiseSeasonPollDto>(),
            cancellationToken);
    }
}

