using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Infrastructure.Clients.Franchise.Queries;

public record GetFranchiseSeasonByIdResponse(FranchiseSeasonDto? Season);
