using Microsoft.Extensions.Logging;

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
        IHttpClientFactory httpClientFactory)
        : base(loggerFactory, httpClientFactory)
    {
    }

    protected override FranchiseClient CreateClient(ILogger<FranchiseClient> logger, HttpClient httpClient)
        => new(logger, httpClient);
}
