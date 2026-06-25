using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
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
///
/// <para>
/// Also acts as the recovery trigger for stuck-Final contests: if scores
/// arrive AFTER the canonical status flipped to STATUS_FINAL but enrichment
/// hadn't yet completed (e.g., enrichment ran early and saw 0-0, then
/// deferred), this handler re-enqueues <see cref="EnrichContestCommand"/>.
/// See docs/ for the 2026-06-24 stuck-Final MLB incident.
/// </para>
/// </summary>
public class CompetitorScoreUpdatedConsumerHandler : ICompetitorScoreUpdatedConsumerHandler
{
    private readonly ILogger<CompetitorScoreUpdatedConsumerHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IEventBus _eventBus;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public CompetitorScoreUpdatedConsumerHandler(
        ILogger<CompetitorScoreUpdatedConsumerHandler> logger,
        TeamSportDataContext dataContext,
        IEventBus eventBus,
        IDateTimeProvider dateTimeProvider,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventBus = eventBus;
        _dateTimeProvider = dateTimeProvider;
        _backgroundJobProvider = backgroundJobProvider;
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

        _logger.LogInformation("CompetitorScoreUpdatedConsumerHandler started.");

        var contest = await _dataContext.Contests
            .FirstOrDefaultAsync(x => x.Id == evt.ContestId);

        if (contest is null)
        {
            _logger.LogWarning("Contest not found for score update.");
            return;
        }

        if (contest.HomeTeamFranchiseSeasonId == evt.FranchiseSeasonId)
        {
            // At-least-once redelivery short-circuit. The upstream score-doc
            // processor only publishes when the persisted score actually
            // changes, but MassTransit / broker retry can still re-deliver the
            // same event. Without this guard, every redelivery would re-stamp
            // ModifiedUtc and re-broadcast ContestScoreChanged to every
            // SignalR-connected client.
            if (contest.HomeScore == evt.Score)
            {
                _logger.LogInformation("HomeScore already current — redelivery no-op.");
                return;
            }

            var previousHomeScore = contest.HomeScore;
            contest.HomeScore = evt.Score;
            _logger.LogInformation(
                "Updated HomeScore. PreviousScore={PreviousScore}, NewScore={NewScore}",
                previousHomeScore, evt.Score);
        }
        else if (contest.AwayTeamFranchiseSeasonId == evt.FranchiseSeasonId)
        {
            if (contest.AwayScore == evt.Score)
            {
                _logger.LogInformation("AwayScore already current — redelivery no-op.");
                return;
            }

            var previousAwayScore = contest.AwayScore;
            contest.AwayScore = evt.Score;
            _logger.LogInformation(
                "Updated AwayScore. PreviousScore={PreviousScore}, NewScore={NewScore}",
                previousAwayScore, evt.Score);
        }
        else
        {
            _logger.LogWarning(
                "FranchiseSeasonId does not match home or away team. HomeTeam={HomeTeam}, AwayTeam={AwayTeam}",
                contest.HomeTeamFranchiseSeasonId, contest.AwayTeamFranchiseSeasonId);
            return;
        }

        contest.ModifiedBy = evt.CorrelationId;
        contest.ModifiedUtc = _dateTimeProvider.UtcNow();

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

        // Stuck-Final recovery: if the contest already saw STATUS_FINAL on
        // the canonical status row but FinalizedUtc never got stamped,
        // enrichment must have deferred (e.g., 0-0 implausible-final guard
        // before scores were sourced). The score we just persisted is the
        // missing input — re-enqueue enrichment now. Idempotent at the
        // enrichment side via the contestAlreadyFinalized && no-unfinalized-
        // odds short-circuit, so multiple score arrivals don't churn.
        if (contest.FinalizedUtc is null)
        {
            var statusTypeName = await _dataContext.CompetitionStatuses
                .AsNoTracking()
                .Join(_dataContext.Competitions,
                      s => s.CompetitionId,
                      c => c.Id,
                      (s, c) => new { s.StatusTypeName, c.ContestId })
                .Where(x => x.ContestId == contest.Id)
                .Select(x => x.StatusTypeName)
                .FirstOrDefaultAsync();

            if (statusTypeName == "STATUS_FINAL")
            {
                var cmd = new EnrichContestCommand(contest.Id, evt.CorrelationId);
                _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));

                _logger.LogInformation(
                    "Re-enqueued EnrichContestCommand — contest status is STATUS_FINAL but FinalizedUtc is null. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contest.Id, evt.CorrelationId);
            }
        }

        // Completion marker only. The event carries one team's score; the
        // mid-flow "Updated HomeScore. HomeScore=X" / "Updated AwayScore.
        // AwayScore=X" log line above already records which side changed
        // and to what value. Including contest.HomeScore + contest.AwayScore
        // here implied both sides were updated — leave the contest-wide
        // scoreboard out of this log to avoid the misread.
        _logger.LogInformation("CompetitorScoreUpdatedConsumerHandler completed.");
    }
}
