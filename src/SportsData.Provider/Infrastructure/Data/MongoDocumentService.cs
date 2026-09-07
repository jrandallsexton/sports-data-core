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

        /// <summary>
        /// Returns the subset of <paramref name="ids"/> whose documents are
        /// both persisted AND have been published downstream at least once
        /// (LastPublishedUtc set — written only after a successful
        /// DocumentCreated publish) — one batched primary-key read. Used by
        /// the live-index already-seen skip (L2): a document that persisted
        /// but whose publish never landed is deliberately NOT returned, so it
        /// re-enqueues and the cache-hit path republishes it.
        /// <paramref name="collectionName"/> is the Mongo (per-DocumentType)
        /// collection; the Cosmos implementation ignores it and queries the
        /// sport-scoped container where all types are co-located, which is
        /// exact because ids are globally unique SourceUrlHashes.
        /// See docs/features/live-sourcing-already-seen-skip.md.
        /// </summary>
        Task<HashSet<string>> GetPublishedIdsAsync(string collectionName, IReadOnlyCollection<string> ids);

        Task InsertOneAsync<T>(string collectionName, T document) where T : IHasSourceUrl;

        Task ReplaceOneAsync<T>(string collectionName, string id, T document) where T : IHasSourceUrl;

        Task UpdateFieldAsync<T>(string collectionName, string id, string fieldName, object? value);

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
            var collection = _database.GetCollection<T>(collectionName);
            var cursor = await collection.FindAsync(filter);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task<HashSet<string>> GetPublishedIdsAsync(string collectionName, IReadOnlyCollection<string> ids)
        {
            if (ids.Count == 0)
                return new HashSet<string>();

            // BsonDocument on "_id" (rather than a typed filter) so the query
            // is a pure primary-key $in with an _id-only projection — the
            // driver maps DocumentBase.Id to _id by convention, and Id is
            // assigned from SourceUrlHash on insert.
            //
            // The LastPublishedUtc $ne null clause is the "published at least
            // once" half of the contract: Mongo's $ne null excludes documents
            // where the field is null OR missing, so a doc that persisted but
            // whose publish never landed (and pre-marker legacy docs) is not
            // returned — it re-enqueues and the cache-hit path republishes it,
            // which then writes the marker.
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            var filter = Builders<BsonDocument>.Filter.In("_id", ids)
                & Builders<BsonDocument>.Filter.Ne(nameof(DocumentBase.LastPublishedUtc), BsonNull.Value);

            var found = await collection
                .Find(filter)
                .Project(Builders<BsonDocument>.Projection.Include("_id"))
                .ToListAsync();

            return found.Select(d => d["_id"].AsString).ToHashSet();
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

            try
            {
                await collection.InsertOneAsync(document);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Safe to treat as success: _id is a full SHA-256 hash of the normalized URL
                // (scheme + host + path, lowercased, no query string — see HashProvider.GenerateHashFromUri).
                // Collections are scoped per sport database, so a duplicate _id within the same collection
                // means another pod/worker inserted the identical document from the same source URL.
                _logger.LogInformation(
                    "Document already exists (concurrent insert by another process), skipping. SourceUrlHash={SourceUrlHash}",
                    document.SourceUrlHash);
            }
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

        public async Task UpdateFieldAsync<T>(string collectionName, string id, string fieldName, object? value)
        {
            var collection = _database.GetCollection<T>(collectionName);
            var filter = Builders<T>.Filter.Eq("_id", id);
            var update = Builders<T>.Update.Set(fieldName, value);
            await collection.UpdateOneAsync(filter, update);
        }

        public bool CanConnect()
        {
            return !string.IsNullOrEmpty(_database?.DatabaseNamespace?.DatabaseName);
        }
    }
}
