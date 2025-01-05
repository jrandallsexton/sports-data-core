using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;
using System.Threading.Tasks;
using SportsData.Core.Models.Canonical;

namespace SportsData.Core.Infrastructure.Clients.Producer
{
    public interface IProvideProducers : IProvideHealthChecks
    {
        Task<VenueCanonicalModel> GetVenue(string id);
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

        public Task<VenueCanonicalModel> GetVenue(string id)
        {
            throw new System.NotImplementedException();
        }
    }
}
