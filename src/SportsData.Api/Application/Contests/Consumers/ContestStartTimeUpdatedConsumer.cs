using MassTransit;

using Microsoft.EntityFrameworkCore;
using SportsData.Api.Application.Leagues.Jobs.LeagueJoinExpiry;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Contests.Consumers
{
    /// <summary>
    /// Keeps PickemGroupMatchup start times in step with Producer's
    /// <see cref="ContestStartTimeUpdated"/>.
    ///
    /// Known trade-off: the kebab-case endpoint formatter strips the
    /// "Consumer" suffix, so this queue is "contest-start-time-updated" — the
    /// same name as the Producer's own ContestStartTimeUpdatedConsumer. In
    /// production that is harmless (API broker vs. the sport's Producer
    /// broker, bridged by exchange-level shovels). On the single local docker
    /// broker the two are competing consumers on one queue, so locally each
    /// start-time change reaches only one of them. Accepted; a
    /// ConsumerDefinition endpoint name is the fix if a distinct queue is ever
    /// needed.
    /// </summary>
    public class ContestStartTimeUpdatedConsumer : IConsumer<ContestStartTimeUpdated>
    {
        private readonly ILogger<ContestStartTimeUpdatedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ILeagueJoinExpiryCalculator _joinExpiryCalculator;

        public ContestStartTimeUpdatedConsumer(
            ILogger<ContestStartTimeUpdatedConsumer> logger,
            AppDataContext dataContext,
            ILeagueJoinExpiryCalculator joinExpiryCalculator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _joinExpiryCalculator = joinExpiryCalculator;
        }

        public async Task Consume(ConsumeContext<ContestStartTimeUpdated> context)
        {
            var msg = context.Message;

            var matchups = await _dataContext.PickemGroupMatchups
                .Where(m => m.ContestId == msg.ContestId)
                .ToListAsync(context.CancellationToken);

            var saveChanges = false;
            foreach (var matchup in matchups)
            {
                if (matchup.StartDateUtc != msg.NewStartTime)
                {
                    _logger.LogInformation("PickemGroupMatchup start time updated. {OldTime}, {NewTime}", matchup.StartDateUtc, msg.NewStartTime);
                    matchup.StartDateUtc = msg.NewStartTime;
                    matchup.ModifiedBy = msg.CorrelationId;
                    matchup.ModifiedUtc = DateTime.UtcNow;
                    saveChanges = true;
                }
            }

            if (saveChanges)
            {
                await _dataContext.SaveChangesAsync(context.CancellationToken);

                // A moved kickoff can shift a league's join expiry (first-game
                // and drop-week expiries derive from these times). Stored
                // values must follow — this handler is the freshness hook the
                // design doc's stored-DateTime decision relies on.
                foreach (var groupId in matchups.Select(m => m.GroupId).Distinct())
                {
                    // Per-group isolation, and deliberately NOT rethrown: the
                    // start times are already persisted, so a MassTransit
                    // retry would find saveChanges == false and skip this
                    // whole block -- the failed recompute would be stranded
                    // until the hourly sweep anyway. Log and let the sweep
                    // self-heal instead of faulting the message.
                    try
                    {
                        await _joinExpiryCalculator.RecomputeAsync(groupId, context.CancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Join-expiry recompute failed for league {GroupId} after start-time update; hourly sweep will self-heal.",
                            groupId);
                    }
                }
            }
        }
    }
}
