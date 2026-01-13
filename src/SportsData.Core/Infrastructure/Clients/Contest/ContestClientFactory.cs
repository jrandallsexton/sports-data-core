using Microsoft.Extensions.Logging;

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
        IHttpClientFactory httpClientFactory)
        : base(loggerFactory, httpClientFactory)
    {
    }

    protected override ContestClient CreateClient(ILogger<ContestClient> logger, HttpClient httpClient)
        => new(logger, httpClient);
}
