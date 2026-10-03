namespace SportsData.Api.Application.Franchises.Seasons.Commands.EnrichFranchiseSeason;

/// <summary>
/// Admin request to make one franchise season current (record enrichment,
/// season statistics refresh, metrics). Slug-addressed like every other
/// route on the franchises controller; the handler resolves it to the
/// Producer's FranchiseSeason id.
/// </summary>
public record EnrichFranchiseSeasonCommand(
    string Sport,
    string League,
    string FranchiseSlugOrId,
    int SeasonYear);
