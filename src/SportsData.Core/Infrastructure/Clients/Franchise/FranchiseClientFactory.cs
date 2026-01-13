using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Config;

using System;
using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Franchise;

public interface IFranchiseClientFactory
{
    IProvideFranchises Resolve(string sport, string league);
}

public class FranchiseClientFactory : ClientFactoryBase<FranchiseClient, IProvideFranchises>, IFranchiseClientFactory
{
    protected override string HttpClientName => HttpClients.FranchiseClient;

    public FranchiseClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(loggerFactory, httpClientFactory, configuration)
    {
    }

    protected override Uri? GetBaseAddressForMode(Sport mode)
    {
        var modeSpecificKey = CommonConfigKeys.GetFranchiseProviderUri(mode);
        var url = Configuration?[modeSpecificKey];

        if (string.IsNullOrEmpty(url))
        {
            var defaultKey = CommonConfigKeys.GetFranchiseProviderUri();
            url = Configuration?[defaultKey];
        }

        return string.IsNullOrEmpty(url) ? null : new Uri(url);
    }

    protected override FranchiseClient CreateClient(ILogger<FranchiseClient> logger, HttpClient httpClient)
        => new(logger, httpClient);
}
