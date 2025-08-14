namespace SportsData.Api.Application.UI.TeamCard.Dtos;

public class TeamCardDto
{
    public required string Slug { get; set; }

    public required string Name { get; set; }

    public required string ShortName { get; set; }

    public int? Ranking { get; set; }

    public required string OverallRecord { get; set; }

    public required string ConferenceRecord { get; set; }

    public required string ColorPrimary { get; set; }

    public required string ColorSecondary { get; set; }

    public required string LogoUrl { get; set; }

    public required string HelmetUrl { get; set; }

    public required string Location { get; set; }

    public required string StadiumName { get; set; }

    public required int StadiumCapacity { get; set; }

    public List<TeamCardNewsItemDto> News { get; set; } = [];

    public List<TeamCardScheduleItemDto> Schedule { get; set; } = [];

    public List<int> SeasonYears { get; set; } = [];
}