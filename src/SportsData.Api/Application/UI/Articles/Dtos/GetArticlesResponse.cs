namespace SportsData.Api.Application.UI.Articles.Dtos;

public class GetArticlesResponse
{
    public int SeasonYear { get; set; }

    public string? SeasonPhase { get; set; }

    public int SeasonWeekNumber { get; set; }

    public List<ArticleSummaryDto> Articles { get; set; } = [];
}
