using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;
using SportsData.Core.Middleware.Health;

using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Venue;

public interface IProvideVenues : IProvideHealthChecks
{
    Task<Result<GetVenuesResponse>> GetVenues();
}

public class VenueProvider : ProviderBase, IProvideVenues
{
    private readonly ILogger<VenueProvider> _logger;

    public VenueProvider(
        ILogger<VenueProvider> logger,
        IHttpClientFactory clientFactory) :
        base(HttpClients.VenueClient, clientFactory)
    {
        _logger = logger;
    }

    public async Task<Result<GetVenuesResponse>> GetVenues()
    {
        var response = await HttpClient.GetAsync("venue");
        response.EnsureSuccessStatusCode();
        var tmp = await response.Content.ReadAsStringAsync();
        var venues = tmp.FromJson<GetVenuesResponse>();
        return new Success<GetVenuesResponse>(venues);
    }
}
