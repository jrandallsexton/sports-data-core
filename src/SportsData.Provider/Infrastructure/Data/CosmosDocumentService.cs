using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using SportsData.Core.Extensions;
using SportsData.Provider.Config;

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
            _defaultContainer = _client.GetContainer(_databaseName, "provider-dev");
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
            _logger.LogInformation("Cosmos querying {@Predicate}", predicate);
            _logger.LogInformation("Cosmos querying {@CollectionName}", collectionName);
            _logger.LogInformation("Cosmos querying {@DatabaseName}", _databaseName);

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

        public async Task InsertOneAsync<T>(string collectionName, T document)
        {
            _logger.LogInformation("Cosmos inserting {@Document}", document);
            var container = _client.GetContainer(_databaseName, collectionName);
            await container.CreateItemAsync(document);
        }

        public bool CanConnect()
        {
            // Optionally do a test container ping here
            return _client != null;
        }
    }
}
