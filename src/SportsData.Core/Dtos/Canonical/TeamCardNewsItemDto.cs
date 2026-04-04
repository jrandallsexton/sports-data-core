namespace SportsData.Core.Dtos.Canonical;

public record TeamCardNewsItemDto
{
    public required string Title { get; init; }

    public required string Link { get; init; }
}
