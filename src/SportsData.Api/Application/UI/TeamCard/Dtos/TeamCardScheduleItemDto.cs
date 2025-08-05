namespace SportsData.Api.Application.UI.TeamCard.Dtos;

public class TeamCardScheduleItemDto
{
    public int Week { get; set; }

    public DateTime Date { get; set; } = default!;

    public string Opponent { get; set; } = default!;

    public string OpponentSlug { get; set; } = default!;

    public string Location { get; set; } = default!;

    public string LocationType { get; set; } = default!;

    public string Result { get; set; } = default!;

    public bool WasWinner { get; set; }
}
