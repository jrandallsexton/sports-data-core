using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Config;

using System;
using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Season;

public interface ISeasonClientFactory
{
    IProvideSeasons Resolve(Sport mode);
}

public class SeasonClientFactory : ClientFactoryBase<SeasonClient, IProvideSeasons>, ISeasonClientFactory
{
    protected override string HttpClientName => HttpClients.SeasonClient;

    public SeasonClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(loggerFactory, httpClientFactory, configuration)
    {
    }

    protected override Uri? GetBaseAddressForMode(Sport mode)
    {
        var modeSpecificKey = CommonConfigKeys.GetSeasonProviderUri(mode);
        var url = Configuration?[modeSpecificKey];

        if (string.IsNullOrEmpty(url))
        {
            var defaultKey = CommonConfigKeys.GetSeasonProviderUri();
            url = Configuration?[defaultKey];
        }

        return string.IsNullOrEmpty(url) ? null : new Uri(url);
    }

    protected override SeasonClient CreateClient(ILogger<SeasonClient> logger, HttpClient httpClient)
        => new(logger, httpClient);
}
