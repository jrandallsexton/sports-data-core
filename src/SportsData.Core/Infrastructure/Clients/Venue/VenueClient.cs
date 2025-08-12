using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Venue.DTOs;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;
using SportsData.Core.Middleware.Health;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FluentValidation.Results;

namespace SportsData.Core.Infrastructure.Clients.Venue;

public interface IProvideVenues : IProvideHealthChecks
{
    Task<Result<GetVenuesResponse>> GetVenues();
    Task<Result<GetVenueByIdResponse>> GetVenueById(string id);
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

    public async Task<Result<GetVenuesResponse>> GetVenues()
    {
        var response = await HttpClient.GetAsync("venues");
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var venues = content.FromJson<List<VenueDto>>() ?? [];

            return new Success<GetVenuesResponse>(new GetVenuesResponse
            {
                Venues = venues
            });
        }

        var failure = content.FromJson<Failure<List<VenueDto>>>();

        var status = failure?.Status ?? ResultStatus.BadRequest;
        var errors = failure?.Errors ?? [new ValidationFailure("Response", "Unknown error deserializing venue data")];

        return new Failure<GetVenuesResponse>(new GetVenuesResponse(), status, errors);
    }

    public async Task<Result<GetVenueByIdResponse>> GetVenueById(string id)
    {
        var response = await HttpClient.GetAsync($"venues/{id}");
        var content = await response.Content.ReadAsStringAsync();

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
