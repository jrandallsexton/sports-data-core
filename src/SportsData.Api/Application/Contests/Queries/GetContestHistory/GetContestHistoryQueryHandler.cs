using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Contests.Queries.GetContestHistory;

/// <summary>
/// Historical context for a matchup, relayed from Producer's
/// preview-history query: last N head-to-head meetings (cross-season) and
/// each team's late-prior-season form. Same data the preview/insight
/// models consume — preview-safe semantics (finalized only, preseason
/// excluded, no as-of leakage) are baked in Producer-side.
/// </summary>
public class GetContestHistoryQueryHandler : IGetContestHistoryQueryHandler
{
    private readonly IContestClientFactory _contestClientFactory;
    private readonly ILogger<GetContestHistoryQueryHandler> _logger;

    public GetContestHistoryQueryHandler(
        IContestClientFactory contestClientFactory,
        ILogger<GetContestHistoryQueryHandler> logger)
    {
        _contestClientFactory = contestClientFactory;
        _logger = logger;
    }

    public async Task<Result<ContestPreviewHistoryDto>> ExecuteAsync(
        GetContestHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(query.Sport, query.League);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex,
                "Unsupported sport/league combination. Sport={Sport}, League={League}",
                query.Sport.Sanitize(), query.League.Sanitize());
            return new Failure<ContestPreviewHistoryDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("Sport/League", ex.Message)]);
        }

        return await _contestClientFactory
            .Resolve(mode)
            .GetContestPreviewHistory(query.ContestId, cancellationToken);
    }
}
