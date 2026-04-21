using System.ComponentModel.DataAnnotations;

namespace SportsData.Api.Application.User.Dtos;

public class UserDto
{
    public Guid Id { get; set; }

    public string? FirebaseUid { get; set; }

    [Required]
    public string Email { get; set; } = null!;

    public string? DisplayName { get; set; }

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
        /// Week numbers that exist for this league, ascending with duplicates removed.
        /// </summary>
        /// <remarks>
        /// The list is guaranteed to be sorted in ascending order and to contain no
        /// duplicate week numbers. Replaces <c>MaxSeasonWeek</c> — custom-window
        /// leagues (e.g. "current week only", or "weeks 5-8") need exact membership,
        /// not a <c>1..N</c> upper bound. Populated by <c>GetMeQueryHandler</c>.
        /// </remarks>
        public IList<int> SeasonWeeks { get; set; } = [];
    }
}
