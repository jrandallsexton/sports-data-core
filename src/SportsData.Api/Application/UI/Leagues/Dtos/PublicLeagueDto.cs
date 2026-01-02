using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class PublicLeagueDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string Commissioner { get; set; } = default!;
        public int RankingFilter { get; set; }
        public int PickType { get; set; }
        public bool UseConfidencePoints { get; set; }
        public int DropLowWeeksCount { get; set; }
    }

}
