using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueWeekOverviewDto
    {
        public List<LeagueWeekMatchupResultDto> Contests { get; set; } = [];

        public List<UserPickDto> UserPicks { get; set; } = [];

        /// <summary>
        /// The league's member roster, ordered by display name. Renderers
        /// must derive matrix columns from THIS list, not from UserPicks:
        /// since reveal enforcement, UserPicks omits other members' picks on
        /// un-locked contests, so a picks-derived column set would drop
        /// members mid-week until one of their games locks.
        /// </summary>
        public List<LeagueWeekMemberDto> Members { get; set; } = [];
    }

    public class LeagueWeekMemberDto
    {
        public Guid UserId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public bool IsSynthetic { get; set; }

        /// <summary>
        /// How many of the week's games this member has submitted a pick for
        /// — locked or not. A COUNT is deliberately the only thing revealed
        /// about un-locked picks (reveal enforcement withholds the picks
        /// themselves): it powers the pre-lock "Who's Ready" readiness list
        /// without leaking what anyone picked.
        /// </summary>
        public int SubmittedPickCount { get; set; }
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
