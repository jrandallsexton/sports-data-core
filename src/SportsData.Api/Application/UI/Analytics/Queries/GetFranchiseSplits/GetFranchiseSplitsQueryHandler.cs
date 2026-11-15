using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Analytics.Queries.GetFranchiseSplits;

public interface IGetFranchiseSplitsQueryHandler
{
    Task<Result<FranchiseSplitsDto>> ExecuteAsync(
        GetFranchiseSplitsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// War Room Splits. Relays to the sport's Producer, which owns the filter
/// rules (rank buckets, pick'em handling, per-filter season floors) and
/// validates the inputs.
/// </summary>
public class GetFranchiseSplitsQueryHandler : IGetFranchiseSplitsQueryHandler
{
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public GetFranchiseSplitsQueryHandler(IFranchiseClientFactory franchiseClientFactory)
    {
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<FranchiseSplitsDto>> ExecuteAsync(
        GetFranchiseSplitsQuery query,
        CancellationToken cancellationToken = default)
    {
        var client = _franchiseClientFactory.Resolve(query.Sport);

        return await client.GetFranchiseSplits(query.Slug, query.Filters, cancellationToken);
    }
}
