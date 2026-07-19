namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueSummaryDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        /// <summary>
        /// Optional commissioner-set blurb, shown under the league name on the
        /// My Leagues cards. Null when the commissioner didn't set one.
        /// </summary>
        public string? Description { get; set; }

        public string Sport { get; set; } = null!;

        /// <summary>
        /// The sport-league the group plays (NCAAF / NFL / MLB / NBA).
        /// Drives the league filter on the My Leagues page.
        /// </summary>
        public string League { get; set; } = null!;

        public string LeagueType { get; set; } = null!;

        public bool UseConfidencePoints { get; set; } = false;

        public int MemberCount { get; set; }

        /// <summary>
        /// The season the league belongs to (e.g. 2026). Drives the leaderboard's
        /// season filter so the client can group leagues by season without
        /// inferring it. Server-authoritative — read straight off
        /// <c>PickemGroup.SeasonYear</c>.
        /// </summary>
        public int SeasonYear { get; set; }

        /// <summary>
        /// Distinct week numbers the league has, ascending. Lets the leaderboard
        /// source its week selector from this one call (including past-season /
        /// deactivated leagues that <c>/user/me</c> omits). Empty for leagues with
        /// no weeks generated yet.
        /// </summary>
        public List<int> SeasonWeeks { get; set; } = [];

        /// <summary>
        /// Set once the group's season has passed. Non-null means the league is
        /// read-only: it cannot be cloned (the clone handler rejects it) and the
        /// UI hides its Duplicate action. Only populated when the caller opts in
        /// via <c>includeDeactivated</c>; otherwise these rows aren't returned.
        /// </summary>
        public DateTime? DeactivatedUtc { get; set; }

        public string? AvatarUrl { get; set; } // optional for visuals
    }
}
