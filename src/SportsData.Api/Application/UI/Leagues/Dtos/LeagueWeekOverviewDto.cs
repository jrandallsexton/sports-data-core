using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueWeekOverviewDto
    {
        public List<LeagueWeekMatchupResultDto> Contests { get; set; } = [];

        public List<UserPickDto> UserPicks { get; set; } = [];
    }

    public class LeagueWeekMatchupResultDto : ContestResultDto
    {
        public Guid? LeagueWinnerFranchiseSeasonId { get; set; }

        public LeagueWeekMatchupResultDto(ContestResultDto baseDto)
        {
            StartDateUtc = baseDto.StartDateUtc;
            ContestId = baseDto.ContestId;
            IsLocked = baseDto.IsLocked;

            // Teams
            AwayShort = baseDto.AwayShort;
            AwayFranchiseSeasonId = baseDto.AwayFranchiseSeasonId;
            AwaySlug = baseDto.AwaySlug;
            AwayRank = baseDto.AwayRank;

            HomeShort = baseDto.HomeShort;
            HomeFranchiseSeasonId = baseDto.HomeFranchiseSeasonId;
            HomeSlug = baseDto.HomeSlug;
            HomeRank = baseDto.HomeRank;

            // Odds
            AwaySpread = baseDto.AwaySpread;
            HomeSpread = baseDto.HomeSpread;
            OverUnder = baseDto.OverUnder;

            // Result
            FinalizedUtc = baseDto.FinalizedUtc;
            AwayScore = baseDto.AwayScore;
            HomeScore = baseDto.HomeScore;
            WinnerFranchiseSeasonId = baseDto.WinnerFranchiseSeasonId;
            SpreadWinnerFranchiseSeasonId = baseDto.SpreadWinnerFranchiseSeasonId;
            OverUnderResult = baseDto.OverUnderResult;
            CompletedUtc = baseDto.CompletedUtc;
        }
    }
}
