using Microsoft.Extensions.Options;

using MongoDB.Driver;

using SportsData.Provider.Config;

namespace SportsData.Provider.Infrastructure.Data
{
    public class DocumentService
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

        public IMongoDatabase Database => _database;
    }
}
