using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Config;

using System;
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
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(loggerFactory, httpClientFactory, configuration)
    {
    }

    protected override Uri? GetBaseAddressForMode(Sport mode)
    {
        var modeSpecificKey = CommonConfigKeys.GetVenueProviderUri(mode);
        var url = Configuration?[modeSpecificKey];

        if (string.IsNullOrEmpty(url))
        {
            var defaultKey = CommonConfigKeys.GetVenueProviderUri();
            url = Configuration?[defaultKey];
        }

        return string.IsNullOrEmpty(url) ? null : new Uri(url);
    }

    protected override VenueClient CreateClient(ILogger<VenueClient> logger, HttpClient httpClient)
        => new(logger, httpClient);
}
