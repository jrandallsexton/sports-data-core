using Microsoft.Extensions.Options;

using MongoDB.Driver;

using SportsData.Provider.Config;

using System.Linq.Expressions;

namespace SportsData.Provider.Infrastructure.Data
{
    public interface IDocumentStore
    {
        IMongoCollection<T> GetCollection<T>(string collectionName);

        public Task<List<T>> GetAllDocumentsAsync<T>(string collectionName);

        Task<T?> GetFirstOrDefaultAsync<T>(string collectionName, Expression<Func<T, bool>> filter);

        Task InsertOneAsync<T>(string collectionName, T document);

        bool CanConnect();
    }

    public class DocumentService : IDocumentStore
    {
        private readonly IMongoDatabase _database;

        public DocumentService(IOptions<ProviderDocDatabase> options)
        {
            // https://stackoverflow.com/questions/44513786/error-on-mongodb-authentication
            const string mongoDbAuthMechanism = "SCRAM-SHA-1";

            var internalIdentity = new MongoInternalIdentity("admin", options.Value.Username);
            var passwordEvidence = new PasswordEvidence(options.Value.Password);
            var mongoCredential = new MongoCredential(mongoDbAuthMechanism, internalIdentity, passwordEvidence);
            
            var serverAddress = new MongoServerAddress(options.Value.ConnectionString);

            var settings = new MongoClientSettings
            {
                // comment this line below if your mongo doesn't run on secured mode
                Credential = mongoCredential,
                Server = serverAddress
            };

            var mongoClient = new MongoClient(settings);

            _database = mongoClient.GetDatabase(options.Value.DatabaseName);
        }

        public IMongoCollection<T> GetCollection<T>(string collectionName)
            => _database.GetCollection<T>(collectionName);

        public async Task<List<T>> GetAllDocumentsAsync<T>(string collectionName)
        {
            var collection = _database.GetCollection<T>(collectionName);
            var filter = Builders<T>.Filter.Empty;
            var cursor = await collection.FindAsync(filter);
            return await cursor.ToListAsync();
        }

        public async Task<T?> GetFirstOrDefaultAsync<T>(string collectionName, Expression<Func<T, bool>> filter)
        {
            var collection = _database.GetCollection<T>(collectionName);
            var cursor = await collection.FindAsync(filter);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task InsertOneAsync<T>(string collectionName, T document)
        {
            var collection = _database.GetCollection<T>(collectionName);
            await collection.InsertOneAsync(document);
        }

        public bool CanConnect()
        {
            return !string.IsNullOrEmpty(_database.DatabaseNamespace.DatabaseName);
        }
    }
}
