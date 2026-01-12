using SportsData.Core.Dtos.Canonical;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.Clients.Franchise.Queries;

public record GetFranchiseSeasonsResponse
{
    public List<FranchiseSeasonDto> Seasons { get; init; } = [];
}
