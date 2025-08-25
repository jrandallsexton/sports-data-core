﻿using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;

using SportsData.Core.Common.Hashing;
using SportsData.Provider.Config;

using System.Linq.Expressions;

namespace SportsData.Provider.Infrastructure.Data
{
    public class CosmosDocumentService : IDocumentStore
    {
        private readonly ILogger<CosmosDocumentService> _logger;
        private readonly CosmosClient _client;
        private readonly string _databaseName;
        private readonly Container _defaultContainer;

        public CosmosDocumentService(
            ILogger<CosmosDocumentService> logger,
            IOptions<ProviderDocDatabaseConfig> options)
        {
            _logger = logger;
            _logger.LogInformation($"Cosmos began with databaseName: {options.Value.DatabaseName}");
            _databaseName = options.Value.DatabaseName;

            _client = new CosmosClient(options.Value.ConnectionString);
            _defaultContainer = _client.GetContainer(_databaseName, "FootballNcaa"); // TODO: Get from AzAppConfig
        }

        public async Task<List<T>> GetAllDocumentsAsync<T>(string containerName)
        {
            var container = _client.GetContainer(_databaseName, containerName);

            var query = container.GetItemLinqQueryable<T>()
                .ToFeedIterator();

            var results = new List<T>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }


        public async Task<T?> GetFirstOrDefaultAsync<T>(string collectionName, Expression<Func<T, bool>> predicate)
        {
            _logger.LogDebug("Cosmos querying {@Predicate}", predicate);
            _logger.LogDebug("Cosmos querying {@CollectionName}", collectionName);
            _logger.LogDebug("Cosmos querying {@DatabaseName}", _databaseName);

            var container = _client.GetContainer(_databaseName, collectionName);

            var iterator = container.GetItemLinqQueryable<T>()
                .Where(predicate)
                .ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var result = response.FirstOrDefault();
                if (result != null)
                    return result;
            }

            return default;
        }

        public async Task InsertOneAsync<T>(string collectionName, T document) where T : IHasSourceUrl
        {
            if (string.IsNullOrWhiteSpace(document.SourceUrlHash))
            {
                if (string.IsNullOrWhiteSpace(document.Uri.AbsoluteUri))
                    throw new InvalidOperationException("SourceUrlHash is missing and Uri is not provided.");

                document.SourceUrlHash = HashProvider.GenerateHashFromUri(document.Uri);
            }

            var routingKey = document.SourceUrlHash.Substring(0, 3).ToUpperInvariant();

            // Assign routingKey to DocumentBase if needed
            if (document is DocumentBase baseDoc)
            {
                baseDoc.Id = document.SourceUrlHash;
                baseDoc.RoutingKey = routingKey;
            }

            _logger.LogDebug("Cosmos inserting");
            var container = _client.GetContainer(_databaseName, collectionName);

            await container.CreateItemAsync(document, new PartitionKey(routingKey));
        }


        public async Task ReplaceOneAsync<T>(string collectionName, string id, T document) where T : IHasSourceUrl
        {
            if (string.IsNullOrWhiteSpace(document.SourceUrlHash))
            {
                if (string.IsNullOrWhiteSpace(document.Uri.AbsoluteUri))
                    throw new InvalidOperationException("SourceUrlHash is missing and Uri is not provided.");

                document.SourceUrlHash = HashProvider.GenerateHashFromUri(document.Uri);
            }

            var container = _client.GetContainer(_databaseName, collectionName);

            var options = new ItemRequestOptions();

            // Only apply if the document has an ETag
            if (document is DocumentBase docWithEtag && !string.IsNullOrEmpty(docWithEtag.ETag))
            {
                options.IfMatchEtag = docWithEtag.ETag;
            }

            try
            {
                var routingKey = document.SourceUrlHash?.Substring(0, 3).ToUpperInvariant()
                                 ?? throw new InvalidOperationException("Missing SourceUrlHash for routing key.");

                // Use routingKey as the partition key
                await container.ReplaceItemAsync(document, id, new PartitionKey(routingKey), options);

            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                _logger.LogWarning("ETag mismatch when replacing document with ID {Id}. Another update may have occurred.", id);
                throw;
            }
        }


        public bool CanConnect()
        {
            // Optionally do a test container ping here
            return _client != null;
        }
    }
}
