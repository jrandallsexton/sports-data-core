using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueDetailDto
    {
        public required Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public required string PickType { get; set; }

        public bool UseConfidencePoints { get; set; }

        public required string TiebreakerType { get; set; }

        public required string TiebreakerTiePolicy { get; set; }

        public string? RankingFilter { get; set; }

        public List<string> ConferenceSlugs { get; set; } = [];

        public bool IsPublic { get; set; }

        public List<LeagueMemberDto> Members { get; set; } = [];

        public class LeagueMemberDto
        {
            public required Guid UserId { get; set; }
            public required string Username { get; set; }
            public required string Role { get; set; }
        }
    }
}
