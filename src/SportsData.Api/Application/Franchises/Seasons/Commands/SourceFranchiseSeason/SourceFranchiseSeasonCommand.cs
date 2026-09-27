namespace SportsData.Api.Application.Franchises.Seasons.Commands.SourceFranchiseSeason;

/// <summary>
/// Admin request to re-source one franchise season from ESPN (its TeamSeason
/// document and the full child cascade, schedule included). Slug-addressed
/// like every other route on the franchises controller; the handler resolves
/// it to the Producer's FranchiseSeason id.
/// </summary>
public record SourceFranchiseSeasonCommand(
    string Sport,
    string League,
    string FranchiseSlugOrId,
    int SeasonYear);
