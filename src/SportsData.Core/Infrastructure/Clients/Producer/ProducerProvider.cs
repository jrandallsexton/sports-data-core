using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Producer
{
    public interface IProvideProducers : IProvideHealthChecks
    {

    }

    public class ProducerProvider : ProviderBase, IProvideProducers
    {
        private readonly ILogger<ProducerProvider> _logger;

        public ProducerProvider(
            ILogger<ProducerProvider> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.ProducerClient, clientFactory)
        {
            _logger = logger;
        }
    }
}
