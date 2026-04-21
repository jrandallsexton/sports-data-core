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

        // Ascending list of week numbers that exist for this league.
        // Replaces MaxSeasonWeek — custom-window leagues (e.g. "current week only"
        // or "weeks 5-8") need exact membership, not a 1..N upper bound.
        public IList<int> SeasonWeeks { get; set; } = [];
    }
}
