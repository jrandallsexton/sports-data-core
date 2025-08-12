//using Microsoft.Extensions.Logging;

//using SportsData.Core.Middleware.Health;

//using System.Net.Http;

//namespace SportsData.Core.Infrastructure.Clients.Franchise
//{
//    public interface IProvideFranchises : IProvideHealthChecks
//    {

//    }

//    public class FranchiseClient : ClientBase, IProvideFranchises
//    {
//        private readonly ILogger<FranchiseClient> _logger;

//        public FranchiseClient(
//            ILogger<FranchiseClient> logger,
//            IHttpClientFactory clientFactory) :
//            base(HttpClients.FranchiseClient, clientFactory)
//        {
//            _logger = logger;
//        }
//    }
//}
