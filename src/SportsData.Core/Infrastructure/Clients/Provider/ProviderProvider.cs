using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Middleware.Health;

using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Provider
{
    public interface IProvideProviders : IProvideHealthChecks
    {
        Task<string> GetDocumentByIdAsync(SourceDataProvider providerId, DocumentType type, int documentId);
    }

    public class ProviderProvider : ProviderBase, IProvideProviders
    {
        private readonly ILogger<ProviderProvider> _logger;

        public ProviderProvider(
            ILogger<ProviderProvider> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.ProviderClient, clientFactory)
        {
            _logger = logger;
        }

        public async Task<string> GetDocumentByIdAsync(SourceDataProvider providerId, DocumentType type, int documentId)
        {
            var response = await HttpClient.GetAsync($"document/{providerId}/{type}/{documentId}");
            response.EnsureSuccessStatusCode();
            var tmp = await response.Content.ReadAsStringAsync();
            return tmp;
        }
    }
}
