using SportsData.Api.Application.UI.Picks.Advisor.Dtos;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Picks.Advisor.Queries.GetPickAdvice;

public interface IGetPickAdviceQueryHandler
{
    Task<Result<PickAdviceDto>> ExecuteAsync(GetPickAdviceQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only. StatBot's advice for the caller in one league-week; nothing is
/// written. See docs/features/statbot-advisor.md.
/// </summary>
public class GetPickAdviceQueryHandler : IGetPickAdviceQueryHandler
{
    private readonly IPickAdviceService _service;

    public GetPickAdviceQueryHandler(IPickAdviceService service)
    {
        _service = service;
    }

    public Task<Result<PickAdviceDto>> ExecuteAsync(GetPickAdviceQuery query, CancellationToken cancellationToken = default) =>
        _service.BuildAsync(query.UserId, query.LeagueId, query.Week, query.Level, cancellationToken);
}
