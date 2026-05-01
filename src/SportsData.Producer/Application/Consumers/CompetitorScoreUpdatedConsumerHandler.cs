using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Consumers;

public interface ICompetitorScoreUpdatedConsumerHandler
{
    Task Process(CompetitorScoreUpdated evt);
}

/// <summary>
/// Hangfire job that does the actual work for a CompetitorScoreUpdated event:
/// updates Contest.HomeScore / Contest.AwayScore and publishes a downstream
/// ContestScoreChanged integration event for SignalR fan-out.
///
/// Spawned by CompetitorScoreUpdatedConsumer (Ingest) and executed on Worker
/// pods so Ingest stays thin.
/// </summary>
public class CompetitorScoreUpdatedConsumerHandler : ICompetitorScoreUpdatedConsumerHandler
{
    private readonly ILogger<CompetitorScoreUpdatedConsumerHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IEventBus _eventBus;

    public CompetitorScoreUpdatedConsumerHandler(
        ILogger<CompetitorScoreUpdatedConsumerHandler> logger,
        TeamSportDataContext dataContext,
        IEventBus eventBus)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventBus = eventBus;
    }

    public async Task Process(CompetitorScoreUpdated evt)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = evt.CorrelationId,
            ["CausationId"] = evt.CausationId,
            ["ContestId"] = evt.ContestId,
            ["FranchiseSeasonId"] = evt.FranchiseSeasonId,
            ["Score"] = evt.Score,
            ["Sport"] = evt.Sport
        });

        _logger.LogInformation(
            "CompetitorScoreUpdatedConsumerHandler started. ContestId={ContestId}, FranchiseSeasonId={FranchiseSeasonId}, Score={Score}",
            evt.ContestId, evt.FranchiseSeasonId, evt.Score);

        var contest = await _dataContext.Contests
            .FirstOrDefaultAsync(x => x.Id == evt.ContestId);

        if (contest is null)
        {
            _logger.LogWarning(
                "Contest not found for score update. ContestId={ContestId}",
                evt.ContestId);
            return;
        }

        if (contest.HomeTeamFranchiseSeasonId == evt.FranchiseSeasonId)
        {
            contest.HomeScore = evt.Score;
            _logger.LogInformation(
                "Updated HomeScore. ContestId={ContestId}, HomeScore={HomeScore}",
                contest.Id, evt.Score);
        }
        else if (contest.AwayTeamFranchiseSeasonId == evt.FranchiseSeasonId)
        {
            contest.AwayScore = evt.Score;
            _logger.LogInformation(
                "Updated AwayScore. ContestId={ContestId}, AwayScore={AwayScore}",
                contest.Id, evt.Score);
        }
        else
        {
            _logger.LogWarning(
                "FranchiseSeasonId does not match home or away team. ContestId={ContestId}, " +
                "FranchiseSeasonId={FranchiseSeasonId}, HomeTeam={HomeTeam}, AwayTeam={AwayTeam}",
                evt.ContestId, evt.FranchiseSeasonId,
                contest.HomeTeamFranchiseSeasonId, contest.AwayTeamFranchiseSeasonId);
            return;
        }

        contest.ModifiedBy = evt.CorrelationId;
        contest.ModifiedUtc = DateTime.UtcNow;

        // Publish integration event BEFORE SaveChangesAsync so the MassTransit
        // EF Core outbox interceptor flushes the publish in the same transaction
        // as the score update.
        await _eventBus.Publish(new ContestScoreChanged(
            ContestId: evt.ContestId,
            FranchiseSeasonId: evt.FranchiseSeasonId,
            Score: evt.Score,
            Ref: null,
            Sport: evt.Sport,
            SeasonYear: evt.SeasonYear,
            CorrelationId: evt.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionCompetitorScoreDocumentProcessor
        ));

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "CompetitorScoreUpdatedConsumerHandler completed. ContestId={ContestId}, HomeScore={HomeScore}, AwayScore={AwayScore}",
            contest.Id, contest.HomeScore, contest.AwayScore);
    }
}
