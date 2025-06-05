namespace SportsData.Provider.Config
{
    public interface IProviderAppConfig
    {
        bool IsDryRun { get; set; }
        int? MaxResourceIndexItemsToProcess { get; set; }
    }

    public class ProviderAppConfig : IProviderAppConfig
    {
        public bool IsDryRun { get; set; } = false;

        public int? MaxResourceIndexItemsToProcess { get; set; } = 5;
    }
}
