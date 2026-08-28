using MassTransit;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.PlayerLineups.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Player Pick'em finals: when a contest finalizes, every anchored
    /// slot is recomputed one last time from the final statline and
    /// FROZEN (<see cref="Infrastructure.Data.Entities.PlayerLineupSlot.IsScoreFinal"/>)
    /// — later stat events skip frozen slots, so the final number can
    /// never drift. Second consumer on ContestFinalized alongside the
    /// team-pick handler; the existing shovels carry it.
    /// </summary>
    public class PlayerLineupContestFinalizedHandler : IConsumer<ContestFinalized>
    {
        private readonly ILogger<PlayerLineupContestFinalizedHandler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IPlayerLineupScorer _scorer;
        private readonly IHubContext<NotificationHub> _hubContext;

        public PlayerLineupContestFinalizedHandler(
            ILogger<PlayerLineupContestFinalizedHandler> logger,
            AppDataContext dataContext,
            IPlayerLineupScorer scorer,
            IHubContext<NotificationHub> hubContext)
        {
            _logger = logger;
            _dataContext = dataContext;
            _scorer = scorer;
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<ContestFinalized> context)
        {
            var msg = context.Message;

            var slots = await _dataContext.PlayerLineupSlots
                .Include(s => s.Lineup)
                    .ThenInclude(l => l.Slots)
                .Where(s => s.ContestId == msg.ContestId && !s.IsScoreFinal)
                .ToListAsync(context.CancellationToken);

            if (slots.Count == 0) return;

            var lineups = await _scorer.ScoreSlotsAsync(
                msg.Sport, slots, finalize: true, context.CancellationToken);

            // Freeze even when scoring found nothing to price (scorer marks
            // statline-less slots final itself); persist whatever changed.
            await _dataContext.SaveChangesAsync(context.CancellationToken);

            foreach (var lineup in lineups)
            {
                await _hubContext.Clients.All.SendAsync(
                    "PlayerLineupScoreUpdated",
                    new
                    {
                        leagueId = lineup.PickemGroupId,
                        userId = lineup.UserId,
                        seasonYear = lineup.SeasonYear,
                        seasonWeek = lineup.SeasonWeek,
                        totalPoints = lineup.TotalPoints,
                        isFinal = true,
                    },
                    context.CancellationToken);
            }

            _logger.LogInformation(
                "Player lineup slots finalized. ContestId={ContestId} Slots={Slots} Lineups={Lineups}",
                msg.ContestId, slots.Count, lineups.Count);
        }
    }
}
