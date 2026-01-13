using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
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
        var paginationError = ValidatePagination(pageNumber, pageSize);
        if (paginationError is not null)
        {
            return new Failure<GetVenuesResponse>(
                new GetVenuesResponse(),
                ResultStatus.BadRequest,
                [paginationError]);
        }

        return await GetAsync(
            $"venues?pageNumber={pageNumber}&pageSize={pageSize}",
            new GetVenuesResponse(),
            "Response",
            ResultStatus.BadRequest,
            cancellationToken);
    }

    public async Task<Result<GetVenueByIdResponse>> GetVenueById(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new Failure<GetVenueByIdResponse>(
                new GetVenueByIdResponse(null),
                ResultStatus.BadRequest,
                [new ValidationFailure("id", "Venue ID cannot be null or empty")]);
        }

        return await GetAsync<GetVenueByIdResponse, VenueDto>(
            $"venues/{id}",
            venue => new GetVenueByIdResponse(venue),
            new GetVenueByIdResponse(null),
            "Venue",
            ResultStatus.BadRequest,
            cancellationToken);
    }
}
