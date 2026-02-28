using SportsData.Core.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonEnrichment;

public record EnqueueFranchiseSeasonEnrichmentCommand(
    int SeasonYear,
    Sport Sport);
