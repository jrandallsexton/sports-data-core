using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

using SportsData.Core.Common.Hashing;
using SportsData.Provider.Config;

using System.Linq.Expressions;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Extensions;

namespace SportsData.Provider.Infrastructure.Data
{
    public interface IDocumentStore
    {
        public Task<List<T>> GetAllDocumentsAsync<T>(string collectionName);

        /// <summary>
        /// Gets the total count of documents matching the filter.
        /// </summary>
        Task<long> CountDocumentsAsync<T>(string collectionName, Expression<Func<T, bool>> filter);

        /// <summary>
        /// Asynchronously yields documents in batches to avoid loading all documents into memory at once.
        /// This is critical for large collections (1000+ documents) to prevent OutOfMemoryException.
        /// </summary>
        IAsyncEnumerable<List<T>> GetDocumentsInBatchesAsync<T>(string collectionName, int batchSize = 500);

        /// <summary>
        /// Asynchronously yields filtered documents in batches to avoid loading all documents into memory at once.
        /// Applies the filter predicate to retrieve only matching documents.
        /// </summary>
        IAsyncEnumerable<List<T>> GetDocumentsInBatchesAsync<T>(
            string collectionName, 
            Expression<Func<T, bool>> filter, 
            int batchSize = 500);

        Task<T?> GetFirstOrDefaultAsync<T>(string collectionName, Expression<Func<T, bool>> filter);

        Task InsertOneAsync<T>(string collectionName, T document) where T : IHasSourceUrl;

        Task ReplaceOneAsync<T>(string collectionName, string id, T document) where T : IHasSourceUrl;

        bool CanConnect();
    }

    public class MongoDocumentService : IDocumentStore
    {
        private readonly ILogger<MongoDocumentService> _logger;
        private readonly IMongoDatabase _database;
        private readonly IAppMode _mode;

        public MongoDocumentService(
            ILogger<MongoDocumentService> logger,
            IOptions<ProviderDocDatabaseConfig> options,
            IAppMode mode)
        {

            _logger = logger;
            _mode = mode;

            var internalIdentity = new MongoInternalIdentity(options.Value.DatabaseName, options.Value.Username);
            var passwordEvidence = new PasswordEvidence(options.Value.Password);
            var mongoCredential = new MongoCredential("SCRAM-SHA-256", internalIdentity, passwordEvidence);
            var serverAddress = new MongoServerAddress(options.Value.ConnectionString);

            var settings = new MongoClientSettings
            {
                Credential = mongoCredential,
                Server = serverAddress
            };

            var client = new MongoClient(settings);
            _database = client.GetDatabase(options.Value.DatabaseName);
        }

        public async Task<List<T>> GetAllDocumentsAsync<T>(string collectionName)
        {
            var collection = _database.GetCollection<T>(collectionName);
            var filter = Builders<T>.Filter.Empty;
            var cursor = await collection.FindAsync(filter);
            return await cursor.ToListAsync();
        }

        public async Task<long> CountDocumentsAsync<T>(string collectionName, Expression<Func<T, bool>> filter)
        {
            var collection = _database.GetCollection<T>(collectionName);
            return await collection.CountDocumentsAsync(filter);
        }

        /// <summary>
        /// Asynchronously yields documents in batches to avoid loading all documents into memory at once.
        /// This is critical for large collections (1000+ documents) to prevent OutOfMemoryException.
        /// </summary>
        public async IAsyncEnumerable<List<T>> GetDocumentsInBatchesAsync<T>(string collectionName, int batchSize = 500)
        {
            var collection = _database.GetCollection<T>(collectionName);
            var filter = Builders<T>.Filter.Empty;
            
            var options = new FindOptions<T>
            {
                BatchSize = batchSize
            };

            using var cursor = await collection.FindAsync(filter, options);
            
            while (await cursor.MoveNextAsync())
            {
                var batch = cursor.Current.ToList();
                if (batch.Count > 0)
                {
                    yield return batch;
                }
            }
        }

        /// <summary>
        /// Asynchronously yields filtered documents in batches to avoid loading all documents into memory at once.
        /// Applies the filter predicate to retrieve only matching documents.
        /// </summary>
        public async IAsyncEnumerable<List<T>> GetDocumentsInBatchesAsync<T>(
            string collectionName, 
            Expression<Func<T, bool>> filter, 
            int batchSize = 500)
        {
            var collection = _database.GetCollection<T>(collectionName);
            
            var options = new FindOptions<T>
            {
                BatchSize = batchSize
            };

            using var cursor = await collection.FindAsync(filter, options);
            
            while (await cursor.MoveNextAsync())
            {
                var batch = cursor.Current.ToList();
                if (batch.Count > 0)
                {
                    yield return batch;
                }
            }
        }

        public async Task<T?> GetFirstOrDefaultAsync<T>(string collectionName, Expression<Func<T, bool>> filter)
        {
            // TODO: Re-enable logging if needed?
            //_logger.LogInformation("Mongo querying: {Filter}", filter);
            var collection = _database.GetCollection<T>(collectionName);
            var cursor = await collection.FindAsync(filter);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task InsertOneAsync<T>(string collectionName, T document) where T : IHasSourceUrl
        {
            if (string.IsNullOrWhiteSpace(document.SourceUrlHash))
            {
                if (string.IsNullOrWhiteSpace(document.Uri.ToCleanUrl()))
                    throw new InvalidOperationException("SourceUrlHash is missing and Uri is not provided.");
                document.SourceUrlHash = HashProvider.GenerateHashFromUri(document.Uri);
            }

            if (document is DocumentBase baseDoc)
            {
                baseDoc.Id = document.SourceUrlHash;
            }

            var collection = _database.GetCollection<T>(collectionName);
            _logger.LogInformation("Mongo inserting document with SourceUrlHash: {SourceUrlHash}", document.SourceUrlHash);
            await collection.InsertOneAsync(document);
        }

        public async Task ReplaceOneAsync<T>(string collectionName, string id, T document) where T : IHasSourceUrl
        {
            if (string.IsNullOrWhiteSpace(document.SourceUrlHash))
            {
                if (string.IsNullOrWhiteSpace(document.Uri.ToCleanUrl()))
                    throw new InvalidOperationException("SourceUrlHash is missing and Uri is not provided.");
                document.SourceUrlHash = HashProvider.GenerateHashFromUri(document.Uri);
            }

            if (document is DocumentBase baseDoc)
            {
                baseDoc.Id = document.SourceUrlHash;
            }

            var collection = _database.GetCollection<T>(collectionName);
            var filter = Builders<T>.Filter.Eq("_id", id); // Correct filter

            var result = await collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true });

            if (result.MatchedCount > 0)
            {
                _logger.LogInformation("Mongo replaced document with _id (SourceUrlHash): {SourceUrlHash}", id);
            }
            else
            {
                _logger.LogInformation("Mongo inserted new document with _id (SourceUrlHash): {SourceUrlHash}", id);
            }
        }

        public bool CanConnect()
        {
            return !string.IsNullOrEmpty(_database?.DatabaseNamespace?.DatabaseName);
        }
    }
}
