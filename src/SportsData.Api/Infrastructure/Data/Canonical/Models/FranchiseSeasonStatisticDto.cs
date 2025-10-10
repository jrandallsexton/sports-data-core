namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class FranchiseSeasonStatisticDto
    {
        public Dictionary<string, List<FranchiseSeasonStatisticEntry>> Statistics { get; set; } = [];

        public int GamesPlayed { get; set; }
        
        public class FranchiseSeasonStatisticEntry
        {
            public string Category { get; set; } = default!;
            public string StatisticKey { get; set; } = default!;
            public string StatisticValue { get; set; } = default!;
            public string? DisplayValue { get; set; }
            public double? PerGameValue { get; set; }
            public string? PerGameDisplayValue { get; set; }
            public int? Rank { get; set; }
            public bool IsNegativeAttribute { get; set; }
        }
    }
}
