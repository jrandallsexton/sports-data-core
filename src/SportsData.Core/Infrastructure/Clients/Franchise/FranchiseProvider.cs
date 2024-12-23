using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Franchise
{
    public interface IProvideFranchises : IProvideHealthChecks
    {

    }

    public class FranchiseProvider : ProviderBase, IProvideFranchises
    {
        private readonly ILogger<FranchiseProvider> _logger;

        public FranchiseProvider(
            ILogger<FranchiseProvider> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.FranchiseClient, clientFactory)
        {
            _logger = logger;
        }
    }
}
