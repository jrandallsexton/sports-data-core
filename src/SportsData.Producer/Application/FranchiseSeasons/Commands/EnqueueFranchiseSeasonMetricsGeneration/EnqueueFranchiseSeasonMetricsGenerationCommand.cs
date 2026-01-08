using SportsData.Core.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;

public record EnqueueFranchiseSeasonMetricsGenerationCommand(int SeasonYear, Sport Sport);
