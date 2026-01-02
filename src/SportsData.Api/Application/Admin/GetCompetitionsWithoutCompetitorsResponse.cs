using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.Admin;

public class GetCompetitionsWithoutCompetitorsResponse
{
    public List<CompetitionWithoutCompetitorsDto> Items { get; init; } = new();
}
