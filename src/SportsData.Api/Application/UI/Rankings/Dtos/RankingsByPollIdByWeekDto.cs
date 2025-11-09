namespace SportsData.Api.Application.UI.Rankings.Dtos
{
    public record RankingsByPollIdByWeekDto
    {
        public required string PollName { get; init; }

        public required string PollId { get; init; }

        public bool HasPoints { get; init; }

        public bool HasFirstPlaceVotes { get; init; }

        public bool HasTrends { get; init; }

        public int SeasonYear { get; init; }

        public int Week { get; init; }

        public DateTime PollDateUtc { get; init; }

        public List<RankingsByPollIdByWeekEntryDto> Entries { get; init; } = [];

        public record RankingsByPollIdByWeekEntryDto
        {
            public Guid FranchiseSeasonId { get; init; }

            public required string FranchiseSlug { get; init; }

            public required string FranchiseName { get; init; }

            public required string FranchiseLogoUrl { get; init; }

            public int Wins { get; init; }

            public int Losses { get; init; }

            public int Rank { get; init; }

            public int? PreviousRank { get; init; }

            public int Points { get; init; }

            public int? FirstPlaceVotes { get; init; }

            public string? Trend { get; init; }

            public DateTime? PollDateUtc { get; init; }
        }
    }
}
