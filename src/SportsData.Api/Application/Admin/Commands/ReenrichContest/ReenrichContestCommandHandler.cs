using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Admin.Commands.ReenrichContest;

public interface IReenrichContestCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        ReenrichContestCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Admin "re-run enrichment" path for a single contest. Two side effects
/// in sequence:
///   1. API nulls UserPick.ScoredAt / IsCorrect / PointsAwarded for every
///      pick on this contest. This is the picks-side rollback — without
///      it, the existing PickScoringProcessor's "already scored" short-
///      circuit would no-op the re-score.
///   2. API calls Producer's synchronous re-enrich endpoint. Producer
///      clears the derived/enriched fields on its Contest row and re-
///      invokes the enrichment processor inline. When that returns,
///      ContestFinalized has fired and the API's ContestFinalizedHandler
///      has already enqueued ScorePicksCommand for the cleared picks.
///
/// Picks-first ordering is deliberate: if Producer's call fails, picks
/// stay nulled and the next click retries with picks-already-null
/// (idempotent). The reverse — Producer clears its fields but API's
/// picks reset fails — leaves picks scored against an unfinalized
/// contest, which is the actively wrong state we're trying to fix.
///
/// Returns the CorrelationId Producer logged the work under so the UI
/// can surface it for Seq tracing.
/// </summary>
public class ReenrichContestCommandHandler : IReenrichContestCommandHandler
{
    private readonly ILogger<ReenrichContestCommandHandler> _logger;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly AppDataContext _dataContext;

    public ReenrichContestCommandHandler(
        ILogger<ReenrichContestCommandHandler> logger,
        IContestClientFactory contestClientFactory,
        AppDataContext dataContext)
    {
        _logger = logger;
        _contestClientFactory = contestClientFactory;
        _dataContext = dataContext;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        ReenrichContestCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = ActivityExtensions.GetCorrelationId();

        _logger.LogInformation(
            "ReenrichContest initiated. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
            command.ContestId, command.Sport, correlationId);

        // Step 1: null the scored fields on every UserPick for this
        // contest. ExecuteUpdateAsync to avoid pulling rows into memory —
        // picks-per-contest can be in the hundreds across leagues.
        var picksUpdated = await _dataContext.UserPicks
            .Where(p => p.ContestId == command.ContestId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.ScoredAt, (DateTime?)null)
                .SetProperty(p => p.IsCorrect, (bool?)null)
                .SetProperty(p => p.PointsAwarded, (int?)null),
                cancellationToken);

        _logger.LogInformation(
            "ReenrichContest: cleared scored fields on {PickCount} UserPick row(s). ContestId={ContestId}, CorrelationId={CorrelationId}",
            picksUpdated, command.ContestId, correlationId);

        // Step 2: invoke Producer's synchronous re-enrich endpoint.
        var contestClient = _contestClientFactory.Resolve(command.Sport);
        var result = await contestClient.ReenrichContest(command.ContestId, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "ReenrichContest: Producer call failed. ContestId={ContestId}, Sport={Sport}, Status={Status}, CorrelationId={CorrelationId}",
                command.ContestId, command.Sport, result.Status, correlationId);

            var failure = (Failure<Core.Infrastructure.Clients.Contest.Queries.ReenrichContestResponse>)result;
            return new Failure<Guid>(correlationId, result.Status, failure.Errors);
        }

        // Producer returns the CorrelationId it ran under in the body.
        // ActivityExtensions.GetCorrelationId() should yield the same
        // value if traceparent / X-Correlation-Id propagation is healthy,
        // but trust Producer's response when they disagree so the UI shows
        // the id Seq actually has.
        var producerCorrelationId = result.Value.CorrelationId;

        _logger.LogInformation(
            "ReenrichContest completed. ContestId={ContestId}, Sport={Sport}, ProducerCorrelationId={ProducerCorrelationId}, ApiCorrelationId={ApiCorrelationId}",
            command.ContestId, command.Sport, producerCorrelationId, correlationId);

        return new Success<Guid>(producerCorrelationId);
    }
}
