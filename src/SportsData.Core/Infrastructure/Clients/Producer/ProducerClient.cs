//using Microsoft.Extensions.Logging;

//using SportsData.Core.Common;
//using SportsData.Core.Dtos.Canonical;
//using SportsData.Core.Extensions;
//using SportsData.Core.Middleware.Health;

//using System.Net.Http;
//using System.Threading.Tasks;

//namespace SportsData.Core.Infrastructure.Clients.Producer
//{
//    public interface IProvideProducers : IProvideHealthChecks
//    {
//        Task<VenueDto?> GetVenue(string id);
//    }

//    public class ProducerClient : ClientBase, IProvideProducers
//    {
//        private readonly ILogger<ProducerClient> _logger;

//        public ProducerClient(
//            ILogger<ProducerClient> logger,
//            IHttpClientFactory clientFactory) :
//            base(HttpClients.ProducerClient, clientFactory)
//        {
//            _logger = logger;
//        }

//        public async Task<VenueDto?> GetVenue(string id)
//        {
//            var response = await HttpClient.GetAsync($"venue/{id}");
//            response.EnsureSuccessStatusCode();
//            var tmp = await response.Content.ReadAsStringAsync();
//            var venue = tmp.FromJson<Success<VenueDto>>();

//            return venue?.Value;
//        }
//    }
//}
