using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.UI.Map.Dtos;

public class GetMapMatchupsResponse
{
    public List<Matchup> Matchups { get; set; } = [];
}
