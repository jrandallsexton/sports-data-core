using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Extensions
{
    public static class FranchiseSeasonPollDtoExtensions
    {
        public static RankingsByPollIdByWeekDto ToRankingsByPollDto(this FranchiseSeasonPollDto source)
        {
            return new RankingsByPollIdByWeekDto
            {
                PollName = source.PollName,
                PollId = source.PollId,
                HasPoints = source.HasPoints,
                HasFirstPlaceVotes = source.HasFirstPlaceVotes,
                HasTrends = source.HasTrends,
                SeasonYear = source.SeasonYear,
                Week = source.Week,
                PollDateUtc = source.PollDateUtc,
                Entries = source.Entries.Select(e => new RankingsByPollIdByWeekDto.RankingsByPollIdByWeekEntryDto
                {
                    FranchiseSeasonId = e.FranchiseSeasonId,
                    FranchiseSlug = e.FranchiseSlug,
                    FranchiseName = e.FranchiseName,
                    FranchiseLogoUrl = e.FranchiseLogoUrl,
                    Wins = e.Wins,
                    Losses = e.Losses,
                    Rank = e.Rank,
                    PreviousRank = e.PreviousRank,
                    Points = e.Points,
                    FirstPlaceVotes = e.FirstPlaceVotes,
                    Trend = e.Trend,
                    PollDateUtc = source.PollDateUtc // propagated to entry level
                }).ToList()
            };
        }
    }
}
