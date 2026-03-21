namespace SportsData.Provider.Config
{
    [Flags]
    public enum ProviderRole
    {
        Api = 1,
        Ingest = 2,
        Worker = 4,
        All = Api | Ingest | Worker
    }
}
