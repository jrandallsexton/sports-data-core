using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Franchise.Queries;
using SportsData.Core.Middleware.Health;

using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Franchise;

public interface IProvideFranchises : IProvideHealthChecks
{
    Task<Result<GetFranchisesResponse>> GetFranchises(int pageNumber = 1, int pageSize = 50);
    Task<Result<GetFranchiseByIdResponse>> GetFranchiseById(string idOrSlug);
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

    public async Task<Result<GetFranchisesResponse>> GetFranchises(int pageNumber = 1, int pageSize = 50)
    {
        var response = await HttpClient.GetAsync($"franchises?pageNumber={pageNumber}&pageSize={pageSize}");
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var result = content.FromJson<GetFranchisesResponse>();

            if (result == null)
            {
                return new Failure<GetFranchisesResponse>(
                    new GetFranchisesResponse(),
                    ResultStatus.BadRequest,
                    [new ValidationFailure("Response", "Unable to deserialize franchises response")]);
            }

            return new Success<GetFranchisesResponse>(result);
        }

        var failure = content.FromJson<Failure<GetFranchisesResponse>>();

        var status = failure?.Status ?? ResultStatus.BadRequest;
        var errors = failure?.Errors ?? [new ValidationFailure("Response", "Unknown error deserializing franchise data")];

        return new Failure<GetFranchisesResponse>(new GetFranchisesResponse(), status, errors);
    }

    public async Task<Result<GetFranchiseByIdResponse>> GetFranchiseById(string idOrSlug)
    {
        var response = await HttpClient.GetAsync($"franchises/{idOrSlug}");
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var franchise = content.FromJson<FranchiseDto>();

            if (franchise is null)
            {
                return new Failure<GetFranchiseByIdResponse>(
                    new GetFranchiseByIdResponse(null),
                    ResultStatus.BadRequest,
                    [new ValidationFailure("Franchise", $"Unable to deserialize franchise with id or slug '{idOrSlug}'")]
                );
            }

            return new Success<GetFranchiseByIdResponse>(new GetFranchiseByIdResponse(franchise));
        }

        var failure = content.FromJson<Failure<GetFranchiseByIdResponse>>();

        var status = failure?.Status ?? ResultStatus.NotFound;
        var errors = failure?.Errors ?? [new ValidationFailure("Franchise", $"Franchise with id or slug '{idOrSlug}' not found")];

        return new Failure<GetFranchiseByIdResponse>(new GetFranchiseByIdResponse(null), status, errors);
    }
}

