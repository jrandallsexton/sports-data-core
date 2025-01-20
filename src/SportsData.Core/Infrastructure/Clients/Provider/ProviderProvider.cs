﻿using System;
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
        Task<string> GetDocumentByIdAsync(SourceDataProvider providerId, Sport sportId, DocumentType typeId, int documentId, int? seasonId);
        Task PublishDocumentEvents(PublishDocumentEventsCommand command);
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

        public async Task<string> GetDocumentByIdAsync(
            SourceDataProvider providerId,
            Sport sportId,
            DocumentType typeId,
            int documentId,
            int? seasonId)
        {
            var response = await HttpClient.GetAsync($"document/{providerId}/{sportId}/{typeId}/{documentId}/{seasonId}");
            response.EnsureSuccessStatusCode();
            var tmp = await response.Content.ReadAsStringAsync();
            return tmp;
        }

        public async Task PublishDocumentEvents(PublishDocumentEventsCommand command)
        {
            var content = new StringContent(command.ToJson(), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync($"document/publish/", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task PublishDocument(PublishDocumentCommand command)
        {
            throw new NotImplementedException();
        }
    }
}
