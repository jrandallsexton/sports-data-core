namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class FranchiseSeasonRawStat
    {
        public string Category { get; set; } = default!;
        public string Statistic { get; set; } = default!;
        public string DisplayValue { get; set; } = default!;
        public double PerGameValue { get; set; }
        public string PerGameDisplayValue { get; set; } = default!;
        public int Rank { get; set; }
    }
}
