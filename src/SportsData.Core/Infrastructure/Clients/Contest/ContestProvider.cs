using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Contest
{
    public interface IProvideContests : IProvideHealthChecks
    {

    }

    public class ContestProvider : ProviderBase, IProvideContests
    {
        private readonly ILogger<ContestProvider> _logger;

        public ContestProvider(
            ILogger<ContestProvider> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.ContestClient, clientFactory)
        {
            _logger = logger;
        }
    }
}
