namespace SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutDrives;

/// <param name="SeasonYear">Null = each Producer's latest season with a started contest.</param>
public record GetCompetitionsWithoutDrivesQuery(int? SeasonYear = null);
