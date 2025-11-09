namespace SportsData.Api.Application.UI.TeamCard.Dtos;

public record TeamCardNewsItemDto
{
    public required string Title { get; init; }

    public required string Link { get; init; }
}