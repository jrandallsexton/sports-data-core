namespace SportsData.Api.Application.UI.TeamCard.Dtos;

public record TeamCardDto
{
    public Guid FranchiseSeasonId { get; init; }

    public required string Slug { get; init; }

    public required string Name { get; init; }

    public required string ShortName { get; init; }

    public int? Ranking { get; init; }

    public string? Division { get; init; } // TODO

    public string? ConferenceName { get; init; }

    public string? ConferenceShortName { get; init; }

    public string? ConferenceSlug { get; init; }

    public required string OverallRecord { get; init; }

    public required string ConferenceRecord { get; init; }

    public required string ColorPrimary { get; init; }

    public required string ColorSecondary { get; init; }

    public required string LogoUrl { get; init; }

    public required string HelmetUrl { get; init; }

    public required string Location { get; init; }

    public required string StadiumName { get; init; }

    public required int StadiumCapacity { get; init; }

    public List<TeamCardNewsItemDto> News { get; init; } = [];

    public List<TeamCardScheduleItemDto> Schedule { get; init; } = [];

    public List<int> SeasonYears { get; init; } = [];
}