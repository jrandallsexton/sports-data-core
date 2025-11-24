using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.Admin;

public class CompetitionsWithoutDrivesResponse
{
    public List<CompetitionWithoutDrivesDto> Items { get; set; } = [];
}
