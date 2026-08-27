using System.ComponentModel.DataAnnotations;

using SportsData.Core.Common;

namespace SportsData.Api.Application.User.Dtos;

public class UserDto
{
    public Guid Id { get; set; }

    public string? FirebaseUid { get; set; }

    [Required]
    public string Email { get; set; } = null!;

    public string? DisplayName { get; set; }

    public string? Username { get; set; }

    public string? PhotoUrl { get; set; }

    public string? Timezone { get; set; }

    public DateTime LastLoginUtc { get; set; }

    public IList<UserLeagueMembership> Leagues { get; set; } = [];

    public bool IsAdmin { get; set; }

    public bool IsReadOnly { get; set; }

    public class UserLeagueMembership
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        /// <summary>
        /// Optional commissioner-set league description. Null/empty when unset.
        /// Rendered under the league name on YourLeaguesCard.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Sport this league belongs to (FootballNcaa / FootballNfl / BaseballMlb).
        /// Used by the UI to render a default sport icon next to the league name
        /// until commissioner-uploaded league icons land.
        /// </summary>
        public Sport Sport { get; set; }

        /// <summary>
        /// Which game this league plays ("TeamPickem" / "PlayerPickem") —
        /// one game per league. Routes the home-page league card: team
        /// leagues open the picks page, player leagues open the roster
        /// builder.
        /// </summary>
        public string GroupType { get; set; } = null!;

        /// <summary>
        /// Week numbers that exist for this league, ascending with duplicates removed.
        /// </summary>
        /// <remarks>
        /// The list is guaranteed to be sorted in ascending order and to contain no
        /// duplicate week numbers. Replaces <c>MaxSeasonWeek</c> — custom-window
        /// leagues (e.g. "current week only", or "weeks 5-8") need exact membership,
        /// not a <c>1..N</c> upper bound. Populated by <c>GetMeQueryHandler</c>.
        /// </remarks>
        public IList<int> SeasonWeeks { get; set; } = [];

        /// <summary>
        /// The week the user should be picking right now. Smallest <see cref="SeasonWeeks"/>
        /// entry whose matchups still have at least one unstarted game (StartDateUtc &gt; now).
        /// Falls back to the maximum SeasonWeek when the season is fully past so the picks
        /// page lands on the most recent week instead of an arbitrary default. Null only when
        /// the league has no weeks at all.
        /// </summary>
        public int? CurrentSeasonWeek { get; set; }

        /// <summary>
        /// Phase-qualified week identities, ordered phase-then-week. Week
        /// NUMBERS repeat across season phases (an NFL league can hold a
        /// preseason Week 4 AND a regular-season Week 4), so
        /// <see cref="SeasonWeeks"/> alone under-identifies a week; this
        /// list is the full identity the UI routes and renders from
        /// (/picks/phase/{phase}/weeks/{week}). ADDITIVE alongside
        /// SeasonWeeks — mobile keeps consuming the int list untouched.
        /// </summary>
        public IList<LeagueSeasonWeekDetailDto> SeasonWeekDetails { get; set; } = [];

        /// <summary>
        /// SeasonWeekId of the <see cref="CurrentSeasonWeek"/> entry —
        /// disambiguates which phase's week the user should be picking
        /// when numbers collide across phases.
        /// </summary>
        public Guid? CurrentSeasonWeekId { get; set; }
    }
}

/// <summary>
/// One phase-qualified league week. <see cref="Phase"/> is a slug —
/// "preseason" | "regular" | "postseason" (SeasonPhase.TypeCode 1/2/3) —
/// chosen over the int for URL and payload readability. Shared by the
/// GetMe membership payload and the league summary payload.
/// </summary>
public class LeagueSeasonWeekDetailDto
{
    public Guid SeasonWeekId { get; set; }

    public int Week { get; set; }

    public string Phase { get; set; } = null!;
}
