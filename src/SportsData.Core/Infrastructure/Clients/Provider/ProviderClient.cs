using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Middleware.Health;

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Provider
{
    public interface IProvideProviders : IProvideHealthChecks
    {
        Task<string> GetDocumentByUrlHash(string urlHash, CancellationToken cancellationToken = default);

        Task<string> GetDocumentByIdAsync(SourceDataProvider providerId, Sport sportId, DocumentType typeId, long documentId, int? seasonId, CancellationToken cancellationToken = default);

        Task PublishDocumentEvents(PublishDocumentEventsCommand command, CancellationToken cancellationToken = default);

        Task<GetExternalDocumentResponse> GetExternalDocument(GetExternalDocumentQuery query, CancellationToken cancellationToken = default);

        Task<GetExternalImageResponse> GetExternalImage(GetExternalImageQuery query, CancellationToken cancellationToken = default);
    }

    public class ProviderClient : ClientBase, IProvideProviders
    {
        private readonly ILogger<ProviderClient> _logger;

        public ProviderClient(
            ILogger<ProviderClient> logger,
            HttpClient httpClient) : base(httpClient)
        {
            _logger = logger;
        }

        public async Task<string> GetDocumentByUrlHash(string urlHash, CancellationToken cancellationToken = default)
        {
            var url = $"document/urlHash/{urlHash}";

            using var response = await HttpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to obtain document from Provider, status code: {StatusCode}", response.StatusCode);
                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        public async Task<string> GetDocumentByIdAsync(
            SourceDataProvider providerId,
            Sport sportId,
            DocumentType typeId,
            long documentId,
            int? seasonId,
            CancellationToken cancellationToken = default)
        {
            var url = seasonId.HasValue
                ? $"document/{providerId}/{sportId}/{typeId}/{documentId}/{seasonId}"
                : $"document/{providerId}/{sportId}/{typeId}/{documentId}";

            return await GetStringAsync(url, cancellationToken);
        }

        public async Task PublishDocumentEvents(PublishDocumentEventsCommand command, CancellationToken cancellationToken = default)
        {
            await PostAsync("document/publish/", command, cancellationToken);
        }

        public async Task<GetExternalDocumentResponse> GetExternalDocument(GetExternalDocumentQuery query, CancellationToken cancellationToken = default)
        {
            return await PostOrDefaultAsync(
                "document/external/document/",
                query,
                new GetExternalDocumentResponse
                {
                    CanonicalId = string.Empty,
                    Uri = new Uri("about:blank"),
                    Id = string.Empty,
                    IsSuccess = false,
                    Data = string.Empty
                },
                cancellationToken);
        }

        public async Task<GetExternalImageResponse> GetExternalImage(GetExternalImageQuery query, CancellationToken cancellationToken = default)
        {
            return await PostOrDefaultAsync(
                "document/external/image/",
                query,
                new GetExternalImageResponse
                {
                    CanonicalId = string.Empty,
                    Uri = new Uri("about:blank"),
                    Id = string.Empty,
                    IsSuccess = false
                },
                cancellationToken);
        }
    }
}
