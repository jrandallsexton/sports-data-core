namespace SportsData.Api.Application.UI.Articles.Dtos;

public class ArticleDto
{
    public Guid ArticleId { get; set; }

    public Guid ContestId { get; set; }

    public Guid? AwayFranchiseSeasonId { get; set; }

    public Guid? HomeFranchiseSeasonId { get; set; }

    public required string Title { get; set; }

    public required string Content { get; set; }

    public required string Url { get; set; }

    public string[] ImageUrls { get; set; } = [];

    public string? GroupSeasonMap { get; set; }
}
