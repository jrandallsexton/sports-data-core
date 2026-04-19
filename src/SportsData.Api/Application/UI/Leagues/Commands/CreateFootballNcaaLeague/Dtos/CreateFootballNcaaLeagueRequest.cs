namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague.Dtos;

public class CreateFootballNcaaLeagueRequest : CreateLeagueRequestBase
{
    public string? RankingFilter { get; set; }

    public List<string> ConferenceSlugs { get; set; } = [];
}
