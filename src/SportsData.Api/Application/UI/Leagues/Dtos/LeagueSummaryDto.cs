namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueSummaryDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

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
        /// Set once the group's season has passed. Non-null means the league is
        /// read-only: it cannot be cloned (the clone handler rejects it) and the
        /// UI hides its Duplicate action. Only populated when the caller opts in
        /// via <c>includeDeactivated</c>; otherwise these rows aren't returned.
        /// </summary>
        public DateTime? DeactivatedUtc { get; set; }

        public string? AvatarUrl { get; set; } // optional for visuals
    }
}
