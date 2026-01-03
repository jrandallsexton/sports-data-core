using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Consumers;

/// <summary>
/// Handles CompetitorScoreUpdated events to update Contest.HomeScore and Contest.AwayScore in real-time.
/// </summary>
public class CompetitorScoreUpdatedConsumer : IConsumer<CompetitorScoreUpdated>
{
    private readonly FootballDataContext _dataContext;
    private readonly ILogger<CompetitorScoreUpdatedConsumer> _logger;
    private readonly IEventBus _eventBus;

    public CompetitorScoreUpdatedConsumer(
        FootballDataContext dataContext,
        ILogger<CompetitorScoreUpdatedConsumer> logger,
        IEventBus eventBus)
    {
        _dataContext = dataContext;
        _logger = logger;
        _eventBus = eventBus;
    }

    public async Task Consume(ConsumeContext<CompetitorScoreUpdated> context)
    {
        var message = context.Message;

        _logger.LogInformation("Processing CompetitorScoreUpdated event. ContestId={ContestId}, FranchiseSeasonId={FranchiseSeasonId}, Score={Score}",
            message.ContestId,
            message.FranchiseSeasonId,
            message.Score);

        var contest = await _dataContext.Contests
            .FirstOrDefaultAsync(x => x.Id == message.ContestId);

        if (contest is null)
        {
            _logger.LogWarning("Contest not found for score update. ContestId={ContestId}", message.ContestId);
            return;
        }

        // Update the appropriate score based on FranchiseSeasonId
        if (contest.HomeTeamFranchiseSeasonId == message.FranchiseSeasonId)
        {
            contest.HomeScore = message.Score;
            _logger.LogInformation("Updated HomeScore. ContestId={ContestId}, HomeScore={HomeScore}",
                contest.Id,
                message.Score);
        }
        else if (contest.AwayTeamFranchiseSeasonId == message.FranchiseSeasonId)
        {
            contest.AwayScore = message.Score;
            _logger.LogInformation("Updated AwayScore. ContestId={ContestId}, AwayScore={AwayScore}",
                contest.Id,
                message.Score);
        }
        else
        {
            _logger.LogWarning("FranchiseSeasonId does not match home or away team. ContestId={ContestId}, FranchiseSeasonId={FranchiseSeasonId}, HomeTeam={HomeTeam}, AwayTeam={AwayTeam}",
                message.ContestId,
                message.FranchiseSeasonId,
                contest.HomeTeamFranchiseSeasonId,
                contest.AwayTeamFranchiseSeasonId);
            return;
        }

        contest.ModifiedBy = message.CorrelationId;
        contest.ModifiedUtc = DateTime.UtcNow;

        // Publish integration event BEFORE SaveChangesAsync (MassTransit outbox pattern)
        await _eventBus.Publish(new ContestScoreChanged(
            ContestId: message.ContestId,
            FranchiseSeasonId: message.FranchiseSeasonId,
            Score: message.Score,
            CorrelationId: message.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionCompetitorScoreDocumentProcessor
        ));

        _logger.LogInformation("Queued ContestScoreChanged integration event for outbox. ContestId={ContestId}",
            message.ContestId);

        await _dataContext.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Contest score updated and outbox flushed. ContestId={ContestId}, HomeScore={HomeScore}, AwayScore={AwayScore}",
            contest.Id,
            contest.HomeScore,
            contest.AwayScore);
    }
}
