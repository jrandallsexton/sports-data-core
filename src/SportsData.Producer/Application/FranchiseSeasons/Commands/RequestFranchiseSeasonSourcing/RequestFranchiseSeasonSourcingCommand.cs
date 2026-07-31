using SportsData.Core.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestFranchiseSeasonSourcing;

public record RequestFranchiseSeasonSourcingCommand(int SeasonYear, Sport Sport);
