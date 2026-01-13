using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Config;

using System;
using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Producer;

public interface IProducerClientFactory
{
    IProvideProducers Resolve(Sport mode);
}

public class ProducerClientFactory : ClientFactoryBase<ProducerClient, IProvideProducers>, IProducerClientFactory
{
    protected override string HttpClientName => HttpClients.ProducerClient;

    public ProducerClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(loggerFactory, httpClientFactory, configuration)
    {
    }

    protected override Uri? GetBaseAddressForMode(Sport mode)
    {
        // Try mode-specific URL first, fall back to default
        var modeSpecificKey = CommonConfigKeys.GetProducerProviderUri(mode);
        var url = Configuration?[modeSpecificKey];

        if (string.IsNullOrEmpty(url))
        {
            // Fall back to default URL if mode-specific not configured
            var defaultKey = CommonConfigKeys.GetProducerProviderUri();
            url = Configuration?[defaultKey];
        }

        return string.IsNullOrEmpty(url) ? null : new Uri(url);
    }

    protected override ProducerClient CreateClient(ILogger<ProducerClient> logger, HttpClient httpClient)
        => new(logger, httpClient);
}
