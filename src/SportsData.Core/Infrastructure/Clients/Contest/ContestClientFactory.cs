using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Config;

using System;
using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Contest;

public interface IContestClientFactory
{
    IProvideContests Resolve(string sport, string league);
}

public class ContestClientFactory : ClientFactoryBase<ContestClient, IProvideContests>, IContestClientFactory
{
    protected override string HttpClientName => HttpClients.ContestClient;

    public ContestClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(loggerFactory, httpClientFactory, configuration)
    {
    }

    protected override Uri? GetBaseAddressForMode(Sport mode)
    {
        var modeSpecificKey = CommonConfigKeys.GetContestProviderUri(mode);
        var url = Configuration?[modeSpecificKey];

        if (string.IsNullOrEmpty(url))
        {
            var defaultKey = CommonConfigKeys.GetContestProviderUri();
            url = Configuration?[defaultKey];
        }

        return string.IsNullOrEmpty(url) ? null : new Uri(url);
    }

    protected override ContestClient CreateClient(ILogger<ContestClient> logger, HttpClient httpClient)
        => new(logger, httpClient);
}
