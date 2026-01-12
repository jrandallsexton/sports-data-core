using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;
using SportsData.Core.Middleware.Health;

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Venue;

public interface IProvideVenues : IProvideHealthChecks
{
    Task<Result<GetVenuesResponse>> GetVenues(int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<Result<GetVenueByIdResponse>> GetVenueById(string id, CancellationToken cancellationToken = default);
}

public class VenueClient : ClientBase, IProvideVenues
{
    private readonly ILogger<VenueClient> _logger;

    public VenueClient(
        ILogger<VenueClient> logger,
        HttpClient httpClient) :
        base(httpClient)
    {
        _logger = logger;
    }

    public async Task<Result<GetVenuesResponse>> GetVenues(int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        // Input validation
        if (pageNumber < 1)
        {
            return new Failure<GetVenuesResponse>(
                new GetVenuesResponse(),
                ResultStatus.BadRequest,
                [new ValidationFailure("pageNumber", "Page number must be greater than or equal to 1")]);
        }

        if (pageSize < 1)
        {
            return new Failure<GetVenuesResponse>(
                new GetVenuesResponse(),
                ResultStatus.BadRequest,
                [new ValidationFailure("pageSize", "Page size must be greater than or equal to 1")]);
        }

        if (pageSize > 500)
        {
            return new Failure<GetVenuesResponse>(
                new GetVenuesResponse(),
                ResultStatus.BadRequest,
                [new ValidationFailure("pageSize", "Page size must not exceed 500")]);
        }

        using var response = await HttpClient.GetAsync($"venues?pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = content.FromJson<GetVenuesResponse>();

            if (result == null)
            {
                return new Failure<GetVenuesResponse>(
                    new GetVenuesResponse(),
                    ResultStatus.BadRequest,
                    [new ValidationFailure("Response", "Unable to deserialize venues response")]);
            }

            return new Success<GetVenuesResponse>(result);
        }

        var failure = content.FromJson<Failure<GetVenuesResponse>>();

        var status = failure?.Status ?? ResultStatus.BadRequest;
        var errors = failure?.Errors ?? [new ValidationFailure("Response", "Unknown error deserializing venue data")];

        return new Failure<GetVenuesResponse>(new GetVenuesResponse(), status, errors);
    }

    public async Task<Result<GetVenueByIdResponse>> GetVenueById(string id, CancellationToken cancellationToken = default)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(id))
        {
            return new Failure<GetVenueByIdResponse>(
                new GetVenueByIdResponse(null),
                ResultStatus.BadRequest,
                [new ValidationFailure("id", "Venue ID cannot be null or empty")]);
        }

        using var response = await HttpClient.GetAsync($"venues/{id}", cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var venue = content.FromJson<VenueDto>();

            if (venue is null)
            {
                return new Failure<GetVenueByIdResponse>(
                    new GetVenueByIdResponse(null),
                    ResultStatus.BadRequest,
                    [new ValidationFailure("Venue", $"Unable to deserialize venue with id {id}")]
                );
            }

            return new Success<GetVenueByIdResponse>(new GetVenueByIdResponse(venue));
        }

        var failure = content.FromJson<Failure<VenueDto>>();
        var status = failure?.Status ?? ResultStatus.BadRequest;
        var errors = failure?.Errors ?? [new ValidationFailure("Response", $"Unable to retrieve venue with id {id}")];

        return new Failure<GetVenueByIdResponse>(new GetVenueByIdResponse(null), status, errors);
    }
}
