using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Season
{
    public interface IProvideSeasons : IProvideHealthChecks
    {

    }

    public class SeasonClient : ProviderBase, IProvideSeasons
    {
        private readonly ILogger<SeasonClient> _logger;

        public SeasonClient(
            ILogger<SeasonClient> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.SeasonClient, clientFactory)
        {
            _logger = logger;
        }
    }
}
