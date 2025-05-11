namespace SportsData.Provider.Config
{
    // TODO: Split this into classes for MongoDB and CosmosDB
    public class ProviderDocDatabaseConfig
    {
        public string ConnectionString { get; set; } = null!;

        public string DatabaseName { get; set; } = null!;

        public string? Username { get; set; }

        public string? Password { get; set; }

        public string Provider { get; set; } = "Mongo"; // or "Cosmos"
    }
}
