using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Season
{
    public interface IProvideSeasons : IProvideHealthChecks
    {

    }

    public class SeasonProvider : ProviderBase, IProvideSeasons
    {
        private readonly ILogger<SeasonProvider> _logger;

        public SeasonProvider(
            ILogger<SeasonProvider> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.SeasonClient, clientFactory)
        {
            _logger = logger;
        }
    }
}
