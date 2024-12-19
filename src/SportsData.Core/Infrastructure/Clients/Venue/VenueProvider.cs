using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Venue;

public interface IProvideVenues : IProvideHealthChecks
{
    Task<Result<GetVenuesResponse>> GetVenues();
}

public class VenueProvider : IProvideVenues
{
    private readonly ILogger<VenueProvider> _logger;
    private readonly HttpClient _httpClient;

    public VenueProvider(
        ILogger<VenueProvider> logger,
        IHttpClientFactory clientFactory)
    {
        _logger = logger;
        _httpClient = clientFactory.CreateClient(HttpClients.VenueClient);
    }

    public async Task<Result<GetVenuesResponse>> GetVenues()
    {
        // provide HttpClient
        // call Venue Service
        // return
        throw new NotImplementedException();
    }

    public string GetProviderName()
    {
        return HttpClients.VenueClient;
    }

    public async Task<Dictionary<string, object>> GetHealthStatus()
    {
        // TODO: Make this better by using the actual result. Determine a pattern.
        var response = await _httpClient.GetAsync("/health");
        var tmp = response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return new Dictionary<string, object>()
        {
            { "status", response.StatusCode }
        };
    }
}
