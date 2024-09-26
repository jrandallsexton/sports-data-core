namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Teams
{
    public class Item
    {
        public string href { get; set; }
    }

    public class Teams
    {
        public int count { get; set; }
        public int pageIndex { get; set; }
        public int pageSize { get; set; }
        public int pageCount { get; set; }
        public List<Item> items { get; set; }
    }
}
