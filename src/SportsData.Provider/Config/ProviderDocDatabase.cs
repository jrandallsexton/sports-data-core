namespace SportsData.Provider.Config
{
    public class ProviderDocDatabase
    {
        public string ConnectionString { get; set; } = null!;

        public string DatabaseName { get; set; } = null!;

        public string Username { get; set; }

        public string Password { get; set; }
    }
}
