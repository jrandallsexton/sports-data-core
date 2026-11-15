using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise.Queries;

namespace SportsData.Api.Application.UI.Analytics.Queries.GetFranchiseSplits;

public class GetFranchiseSplitsQuery
{
    public required Sport Sport { get; init; }
    public required string Slug { get; init; }
    public required FranchiseSplitsRequest Filters { get; init; }
}
