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

        // League window — null on either side means "open-ended in that direction"
        // (i.e. full-season or no upper bound). Both null = full season.
        public DateTime? StartsOn { get; set; }

        public DateTime? EndsOn { get; set; }

        /// <summary>
        /// Non-null once the league's season has passed: it's read-only. The UI
        /// hides mutating affordances (invite members, delete) when this is set.
        /// </summary>
        public DateTime? DeactivatedUtc { get; set; }

        /// <summary>
        /// Always populated. Clients render "N members" from this rather than
        /// <see cref="Members"/>, which is empty for non-members.
        /// </summary>
        public int MemberCount { get; set; }

        /// <summary>
        /// Whether the caller belongs to this league. False means
        /// <see cref="Members"/> is withheld — see
        /// docs/audit/league-authorization-idor.md.
        /// </summary>
        public bool IsMember { get; set; }

        /// <summary>JoinPolicy enum name, lowercased like the other enums here.</summary>
        public string JoinPolicy { get; set; } = "open";

        /// <summary>
        /// When this league stops (or stopped) accepting members. Derived at
        /// read time from the slate for close-at-first-game leagues; null for
        /// open leagues or an ungenerated slate.
        /// </summary>
        public DateTime? ClosesAtUtc { get; set; }

        /// <summary>False once closed or deactivated — the invite preview and
        /// browse detail render a closed state instead of a Join button.</summary>
        public bool IsJoinable { get; set; }

        /// <summary>
        /// The roster — members only. Empty for non-members (invite preview,
        /// public-league browsing).
        /// </summary>
        public List<LeagueMemberDto> Members { get; set; } = [];

        public class LeagueMemberDto
        {
            public required Guid UserId { get; set; }
            public required string Username { get; set; }
            public required string Role { get; set; }
        }
    }
}
