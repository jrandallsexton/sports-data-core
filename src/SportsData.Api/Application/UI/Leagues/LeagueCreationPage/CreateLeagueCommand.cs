using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.LeagueCreationPage
{
    public class CreateLeagueCommand
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public required PickType PickType { get; set; }

        public required bool UseConfidencePoints { get; set; }

        public required TiebreakerType TiebreakerType { get; set; }

        public required TiebreakerTiePolicy TiebreakerTiePolicy { get; set; }

        public required TeamRankingFilter RankingFilter { get; set; } = TeamRankingFilter.AP_TOP_25;

        public Dictionary<string, Guid> Conferences { get; set; } = new();

        public required bool IsPublic { get; set; }

        // Can be injected via controller from Auth context
        public required Guid CommissionerUserId { get; set; }

        // Hardcoded for now, but required by entity
        public required Sport Sport { get; set; } // e.g. FootballNcaa

        public required League League { get; set; } // e.g. Ncaa

        public required Guid CreatedBy { get; set; }

        public int? DropLowWeeksCount { get; set; }

    }
}
