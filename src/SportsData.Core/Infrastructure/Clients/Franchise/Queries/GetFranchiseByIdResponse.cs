using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Infrastructure.Clients.Franchise.Queries;

public record GetFranchiseByIdResponse(FranchiseDto? Franchise);
