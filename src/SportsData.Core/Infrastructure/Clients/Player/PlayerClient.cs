using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Player
{
    public interface IProvidePlayers : IProvideHealthChecks
    {

    }

    public class PlayerClient : ProviderBase, IProvidePlayers
    {
        private readonly ILogger<PlayerClient> _logger;

        public PlayerClient(
            ILogger<PlayerClient> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.PlayerClient, clientFactory)
        {
            _logger = logger;
        }
    }
}
