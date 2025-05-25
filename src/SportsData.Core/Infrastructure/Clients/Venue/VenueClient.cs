using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Venue.DTOs;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;
using SportsData.Core.Middleware.Health;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Venue;

public interface IProvideVenues : IProvideHealthChecks
{
    Task<Result<GetVenuesResponse>> GetVenues();
    Task<Result<GetVenueByIdResponse>> GetVenueById(int id);
}

public class VenueClient : ProviderBase, IProvideVenues
{
    private readonly ILogger<VenueClient> _logger;

    public VenueClient(
        ILogger<VenueClient> logger,
        IHttpClientFactory clientFactory) :
        base(HttpClients.VenueClient, clientFactory)
    {
        _logger = logger;
    }

    public async Task<Result<GetVenuesResponse>> GetVenues()
    {
        var response = await HttpClient.GetAsync("venues");

        if (response.IsSuccessStatusCode)
        {
            var tmp = await response.Content.ReadAsStringAsync();
            var venues = tmp.FromJson<List<VenueDto>>();
            return new Success<GetVenuesResponse>(new GetVenuesResponse
            {
                Venues = venues
            });
        }

        var t = await response.Content.ReadAsStringAsync();
        var v = t.FromJson<Failure<List<VenueDto>>>();
        return new Failure<GetVenuesResponse>(new GetVenuesResponse(), v.Status, v.Errors);
    }


    public async Task<Result<GetVenueByIdResponse>> GetVenueById(int id)
    {
        var response = await HttpClient.GetAsync($"venues/{id}");

        if (response.IsSuccessStatusCode)
        {
            var tmp = await response.Content.ReadAsStringAsync();
            var venue = tmp.FromJson<VenueDto>();
            return new Success<GetVenueByIdResponse>(new GetVenueByIdResponse()
            {
                Venue = venue
            });
        }

        var t = await response.Content.ReadAsStringAsync();
        var v = t.FromJson<Failure<VenueDto>>();
        return new Failure<GetVenueByIdResponse>(new GetVenueByIdResponse(), v.Status, v.Errors);
    }
}
