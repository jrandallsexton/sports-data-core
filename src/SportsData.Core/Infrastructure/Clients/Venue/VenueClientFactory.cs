using Microsoft.Extensions.Logging;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Venue;

public interface IVenueClientFactory
{
    IProvideVenues Resolve(string sport, string league);
}

public class VenueClientFactory : ClientFactoryBase<VenueClient, IProvideVenues>, IVenueClientFactory
{
    protected override string HttpClientName => HttpClients.VenueClient;

    public VenueClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
        : base(loggerFactory, httpClientFactory)
    {
    }

    protected override VenueClient CreateClient(ILogger<VenueClient> logger, HttpClient httpClient)
        => new(logger, httpClient);
}
