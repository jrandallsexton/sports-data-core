namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague.Dtos;

public class CreateFootballNflLeagueRequest : CreateLeagueRequestBase
{
    public List<string> DivisionSlugs { get; set; } = [];
}
