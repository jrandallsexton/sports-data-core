namespace SportsData.Api.Application.UI.TeamCard.Dtos;

public class TeamCardScheduleItemDto
{
    public Guid ContestId { get; set; }

    public int Week { get; set; }

    public DateTime Date { get; set; } = default!;

    public string Opponent { get; set; } = default!;

    public string OpponentShortName { get; set; } = default!;

    public string OpponentSlug { get; set; } = default!;

    public string Location { get; set; } = default!;

    public string LocationType { get; set; } = default!;

    public DateTime? FinalizedUtc { get; set; }

    public int? AwayScore { get; set; }

    public int? HomeScore { get; set; }

    public bool WasWinner { get; set; }
}
