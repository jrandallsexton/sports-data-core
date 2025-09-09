namespace SportsData.Api.Application.UI.Rankings.Dtos
{
    public class RankingsByPollIdByWeekDto
    {
        public required string PollName { get; set; }

        public int SeasonYear { get; set; }

        public int Week { get; set; }

        public DateTime PollDateUtc { get; set; }

        public List<RankingsByPollIdByWeekEntryDto> Entries { get; set; } = [];

        public class RankingsByPollIdByWeekEntryDto
        {
            public Guid FranchiseSeasonId { get; set; }

            public required string FranchiseSlug { get; set; }

            public required string FranchiseName { get; set; }

            public required string FranchiseLogoUrl { get; set; }

            public int Wins { get; set; }

            public int Losses { get; set; }

            public int Rank { get; set; }

            public int? PreviousRank { get; set; }

            public int Points { get; set; }

            public int? FirstPlaceVotes { get; set; }

            public string? Trend { get; set; }

            public DateTime? PollDateUtc { get; set; }
        }
    }
}
