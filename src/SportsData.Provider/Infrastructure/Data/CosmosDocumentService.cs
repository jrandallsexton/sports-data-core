using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using SportsData.Provider.Config;

namespace SportsData.Provider.Infrastructure.Data
{
    public class CosmosDocumentService : IDocumentStore
    {
        private readonly CosmosClient _client;
        private readonly string _databaseName;
        private readonly Container _defaultContainer;

        public CosmosDocumentService(IOptions<ProviderDocDatabaseConfig> options)
        {
            _databaseName = options.Value.DatabaseName;

            _client = new CosmosClient(options.Value.ConnectionString);
            _defaultContainer = _client.GetContainer(_databaseName, "default");
        }

        public async Task<List<T>> GetAllDocumentsAsync<T>(string collectionName)
        {
            var container = _client.GetContainer(_databaseName, collectionName);
            var query = container.GetItemLinqQueryable<T>(allowSynchronousQueryExecution: false);
            return await Task.FromResult(query.ToList());
        }

        public async Task<T?> GetFirstOrDefaultAsync<T>(string collectionName, Expression<Func<T, bool>> predicate)
        {
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
