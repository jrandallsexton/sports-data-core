using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Player
{
    public interface IProvidePlayers : IProvideHealthChecks
    {

    }

    public class PlayerProvider : ProviderBase, IProvidePlayers
    {
        private readonly ILogger<PlayerProvider> _logger;

        public PlayerProvider(
            ILogger<PlayerProvider> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.PlayerClient, clientFactory)
        {
            _logger = logger;
        }
    }
}
