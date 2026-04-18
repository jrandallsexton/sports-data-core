namespace SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague.Dtos;

public class CreateBaseballMlbLeagueRequest : CreateLeagueRequestBase
{
    public List<string> DivisionSlugs { get; set; } = [];
}
