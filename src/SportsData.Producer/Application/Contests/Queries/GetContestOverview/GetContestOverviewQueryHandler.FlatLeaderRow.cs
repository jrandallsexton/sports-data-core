namespace SportsData.Producer.Application.Contests.Queries.GetContestOverview;

public partial class GetContestOverviewQueryHandler
{
    private class FlatLeaderRow
    {
        public string CategoryId { get; set; } = null!;
        public string CategoryName { get; set; } = null!;
        public string? Abbr { get; set; }
        public string? Unit { get; set; }
        public int DisplayOrder { get; set; }
        public Guid FranchiseSeasonId { get; set; }
        public string PlayerName { get; set; } = null!;
        public string? PlayerHeadshotUrl { get; set; }
        public string StatLine { get; set; } = null!;
        public decimal? Numeric { get; set; }
        public int Rank { get; set; }
        public Guid AthleteSeasonId { get; set; }
    }
}