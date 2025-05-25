using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Contest
{
    public interface IProvideContests : IProvideHealthChecks
    {

    }

    public class ContestClient : ProviderBase, IProvideContests
    {
        private readonly ILogger<ContestClient> _logger;

        public ContestClient(
            ILogger<ContestClient> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.ContestClient, clientFactory)
        {
            _logger = logger;
        }
    }
}
