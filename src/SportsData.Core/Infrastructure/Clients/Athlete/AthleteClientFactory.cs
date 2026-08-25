using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Config;

using System;
using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Athlete;

public interface IAthleteClientFactory
{
    IProvideAthletes Resolve(Sport mode);
}

public class AthleteClientFactory : ClientFactoryBase<AthleteClient, IProvideAthletes>, IAthleteClientFactory
{
    protected override string HttpClientName => HttpClients.AthleteClient;

    public AthleteClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(loggerFactory, httpClientFactory, configuration)
    {
    }

    protected override Uri? GetBaseAddressForMode(Sport mode)
    {
        // Deliberately the FRANCHISE ApiUrl keys: athlete endpoints live on
        // the same Producer instance the franchise reads target, and a
        // parallel AthleteClientConfig would mean provisioning identical
        // URLs in AppConfig for every environment/mode for no routing gain.
        // If athlete reads ever move to their own service, this is the one
        // method to change.
        var modeSpecificKey = CommonConfigKeys.GetFranchiseProviderUri(mode);
        var url = Configuration?[modeSpecificKey];

        if (string.IsNullOrEmpty(url))
        {
            var defaultKey = CommonConfigKeys.GetFranchiseProviderUri();
            url = Configuration?[defaultKey];
        }

        return string.IsNullOrEmpty(url) ? null : new Uri(url);
    }

    protected override AthleteClient CreateClient(ILogger<AthleteClient> logger, HttpClient httpClient)
        => new(logger, httpClient);
}
