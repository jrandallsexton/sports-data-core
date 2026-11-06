using SportsData.Core.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestConferenceMembershipSourcing;

public record RequestConferenceMembershipSourcingCommand(
    int SeasonYear,
    Sport Sport,
    Guid CorrelationId);
