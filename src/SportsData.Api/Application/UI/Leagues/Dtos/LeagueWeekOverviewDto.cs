using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueWeekOverviewDto
    {
        public List<ContestResultDto> Contests { get; set; } = [];

        public List<UserPickDto> UserPicks { get; set; } = [];
    }
}
