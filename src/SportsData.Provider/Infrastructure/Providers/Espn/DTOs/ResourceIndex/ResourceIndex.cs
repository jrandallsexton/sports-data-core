namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.ResourceIndex
{
    public class ResourceIndex
    {
        public int count { get; set; }
        public int pageIndex { get; set; }
        public int pageSize { get; set; }
        public int pageCount { get; set; }
        public List<Item> items { get; set; }
    }
}
