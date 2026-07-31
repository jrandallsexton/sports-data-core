using SportsData.Api.Application.Common.Enums;
using SportsData.Core.Common;

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

        public Sport Sport { get; set; }
        public League League { get; set; }
        public int SeasonYear { get; set; }
        public int MemberCount { get; set; }

        /// <summary>League window; both null = full season.</summary>
        public DateTime? StartsOn { get; set; }
        public DateTime? EndsOn { get; set; }

        /// <summary>Enum names (not ints like PickType) — consumed directly
        /// by the join-confirmation dialog.</summary>
        public string TiebreakerType { get; set; } = default!;
        public string TiebreakerTiePolicy { get; set; } = default!;

        public JoinPolicy JoinPolicy { get; set; }

        /// <summary>
        /// When this league stops (or stopped) accepting members. Derived at
        /// read time from the slate for CloseAtFirstGame; null for Open or
        /// when the slate hasn't been generated yet.
        /// </summary>
        public DateTime? ClosesAtUtc { get; set; }

        /// <summary>False once closed — clients badge instead of hiding, so a
        /// nearly-started league still advertises itself.</summary>
        public bool IsJoinable { get; set; }
    }
}
