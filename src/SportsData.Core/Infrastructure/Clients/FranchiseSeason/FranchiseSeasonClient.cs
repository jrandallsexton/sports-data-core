using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.FranchiseSeason.Queries;
using SportsData.Core.Middleware.Health;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.FranchiseSeason;

public interface IProvideFranchiseSeasons : IProvideHealthChecks
{
    Task<Result<GetFranchiseSeasonsResponse>> GetFranchiseSeasons(Guid franchiseId);
    Task<Result<GetFranchiseSeasonByIdResponse>> GetFranchiseSeasonById(Guid franchiseId, int seasonYear);
}

public class FranchiseSeasonClient : ClientBase, IProvideFranchiseSeasons
{
    private readonly ILogger<FranchiseSeasonClient> _logger;

    public FranchiseSeasonClient(
        ILogger<FranchiseSeasonClient> logger,
        HttpClient httpClient) :
        base(httpClient)
    {
        _logger = logger;
    }

    public async Task<Result<GetFranchiseSeasonsResponse>> GetFranchiseSeasons(Guid franchiseId)
    {
        var response = await HttpClient.GetAsync($"franchises/{franchiseId}/seasons");
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var seasons = content.FromJson<List<Core.Dtos.Canonical.FranchiseSeasonDto>>();

            if (seasons == null)
            {
                return new Failure<GetFranchiseSeasonsResponse>(
                    new GetFranchiseSeasonsResponse(),
                    ResultStatus.BadRequest,
                    [new ValidationFailure("Response", "Unable to deserialize franchise seasons response")]);
            }

            return new Success<GetFranchiseSeasonsResponse>(new GetFranchiseSeasonsResponse { Seasons = seasons });
        }

        var failure = content.FromJson<Failure<GetFranchiseSeasonsResponse>>();

        var status = failure?.Status ?? ResultStatus.BadRequest;
        var errors = failure?.Errors ?? [new ValidationFailure("Response", "Unknown error deserializing franchise season data")];

        return new Failure<GetFranchiseSeasonsResponse>(new GetFranchiseSeasonsResponse(), status, errors);
    }

    public async Task<Result<GetFranchiseSeasonByIdResponse>> GetFranchiseSeasonById(Guid franchiseId, int seasonYear)
    {
        var response = await HttpClient.GetAsync($"franchises/{franchiseId}/seasons/{seasonYear}");
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var season = content.FromJson<Core.Dtos.Canonical.FranchiseSeasonDto>();

            if (season == null)
            {
                return new Failure<GetFranchiseSeasonByIdResponse>(
                    new GetFranchiseSeasonByIdResponse(null),
                    ResultStatus.BadRequest,
                    [new ValidationFailure("Response", "Unable to deserialize franchise season response")]);
            }

            return new Success<GetFranchiseSeasonByIdResponse>(new GetFranchiseSeasonByIdResponse(season));
        }

        var failure = content.FromJson<Failure<GetFranchiseSeasonByIdResponse>>();

        var status = failure?.Status ?? ResultStatus.BadRequest;
        var errors = failure?.Errors ?? [new ValidationFailure("Response", "Unknown error deserializing franchise season data")];

        return new Failure<GetFranchiseSeasonByIdResponse>(new GetFranchiseSeasonByIdResponse(null), status, errors);
    }
}
