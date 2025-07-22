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

        Task<T?> GetFirstOrDefaultAsync<T>(string collectionName, Expression<Func<T, bool>> filter);

        Task InsertOneAsync<T>(string collectionName, T document) where T : IHasSourceUrl;

        Task ReplaceOneAsync<T>(string collectionName, string id, T document) where T : IHasSourceUrl;

        bool CanConnect();
    }

    public class MongoDocumentService : IDocumentStore
    {
        private readonly ILogger<MongoDocumentService> _logger;
        private readonly IMongoCollection<BsonDocument> _rawCollection;
        private readonly IMongoDatabase _database;
        private readonly IAppMode _mode;

        public MongoDocumentService(
            ILogger<MongoDocumentService> logger,
            IOptions<ProviderDocDatabaseConfig> options,
            IAppMode mode)
        {
            _logger = logger;
            _mode = mode;

            var internalIdentity = new MongoInternalIdentity("admin", options.Value.Username);
            var passwordEvidence = new PasswordEvidence(options.Value.Password);
            var mongoCredential = new MongoCredential("SCRAM-SHA-1", internalIdentity, passwordEvidence);
            var serverAddress = new MongoServerAddress(options.Value.ConnectionString);

            var settings = new MongoClientSettings
            {
                Credential = mongoCredential,
                Server = serverAddress
            };

            var client = new MongoClient(settings);
            _database = client.GetDatabase(options.Value.DatabaseName);
            _rawCollection = _database.GetCollection<BsonDocument>(_mode.CurrentSport.ToString());
        }

        public async Task<List<T>> GetAllDocumentsAsync<T>(string collectionName)
        {
            var collection = _database.GetCollection<T>(collectionName);
            var filter = Builders<T>.Filter.Empty;
            var cursor = await collection.FindAsync(filter);
            return await cursor.ToListAsync();
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
                baseDoc.RoutingKey = document.SourceUrlHash.Substring(0, 3).ToUpperInvariant();
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
                baseDoc.RoutingKey = document.SourceUrlHash.Substring(0, 3).ToUpperInvariant();
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
