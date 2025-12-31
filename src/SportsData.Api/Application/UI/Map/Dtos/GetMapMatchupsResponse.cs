using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.UI.Map.Dtos;

public class GetMapMatchupsResponse
{
    public List<Matchup> Matchups { get; set; } = [];
}
