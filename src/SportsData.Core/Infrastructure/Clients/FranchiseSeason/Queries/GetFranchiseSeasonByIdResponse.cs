using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Infrastructure.Clients.FranchiseSeason.Queries;

public record GetFranchiseSeasonByIdResponse(FranchiseSeasonDto? Season);
