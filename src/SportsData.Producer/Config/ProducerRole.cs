namespace SportsData.Producer.Config
{
    [Flags]
    public enum ProducerRole
    {
        Api = 1,
        Ingest = 2,
        Worker = 4,
        All = Api | Ingest | Worker
    }
}
