using System;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Middleware.Health;

using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Provider
{
    public interface IProvideProviders : IProvideHealthChecks
    {
        Task<string> GetDocumentByUrlHash(string urlHash);

        Task<string> GetDocumentByIdAsync(SourceDataProvider providerId, Sport sportId, DocumentType typeId, long documentId, int? seasonId);

        Task PublishDocumentEvents(PublishDocumentEventsCommand command);

        Task<GetExternalDocumentQueryResponse> GetExternalDocument(GetExternalDocumentQuery command);
    }

    public class ProviderClient : ClientBase, IProvideProviders
    {
        private readonly ILogger<ProviderClient> _logger;

        public ProviderClient(
            ILogger<ProviderClient> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.ProviderClient, clientFactory)
        {
            _logger = logger;
        }

        public async Task<string> GetDocumentByUrlHash(string urlHash)
        {
            var url = $"document/urlHash/{urlHash}";
            _logger.LogInformation("Using {@BaseAddress} with {@Url}", HttpClient.BaseAddress, url);

            try
            {
                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var tmp = await response.Content.ReadAsStringAsync();
                return tmp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to obtain document from Provider");
                throw;
            }
        }

        public async Task<string> GetDocumentByIdAsync(
            SourceDataProvider providerId,
            Sport sportId,
            DocumentType typeId,
            long documentId,
            int? seasonId)
        {
            var url = seasonId.HasValue ?
                $"document/{providerId}/{sportId}/{typeId}/{documentId}/{seasonId}" :
                $"document/{providerId}/{sportId}/{typeId}/{documentId}";
            _logger.LogInformation("Using {@BaseAddress} with {@Url}", HttpClient.BaseAddress, url);

            try
            {
                var response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var tmp = await response.Content.ReadAsStringAsync();
                return tmp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to obtain document from Provider");
                throw;
            }
        }

        public async Task PublishDocumentEvents(PublishDocumentEventsCommand command)
        {
            var content = new StringContent(command.ToJson(), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync($"document/publish/", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<GetExternalDocumentQueryResponse> GetExternalDocument(GetExternalDocumentQuery query)
        {
            var content = new StringContent(query.ToJson(), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync($"document/external/", content);
            response.EnsureSuccessStatusCode();
            var tmp = await response.Content.ReadAsStringAsync();
            return tmp.FromJson<GetExternalDocumentQueryResponse>();
        }
    }
}
