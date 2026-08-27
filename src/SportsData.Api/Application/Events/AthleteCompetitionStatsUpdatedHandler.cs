using MassTransit;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.PlayerLineups.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Athletes;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// The precise Player Pick'em scoring trigger: Producer's stat
    /// document processor announces a rewritten statline, and this
    /// consumer recomputes exactly the lineup slots anchored to that
    /// (contest, athleteSeason) — persists points + lineup totals and
    /// broadcasts <c>PlayerLineupScoreUpdated</c>. No polling, no jobs.
    /// Slots frozen at finalization are skipped. Requires the paired
    /// per-sport shovels in sports-data-config (football only — the
    /// event is not published for MLB).
    /// </summary>
    public class AthleteCompetitionStatsUpdatedHandler : IConsumer<AthleteCompetitionStatsUpdated>
    {
        private readonly ILogger<AthleteCompetitionStatsUpdatedHandler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IPlayerLineupScorer _scorer;
        private readonly IHubContext<NotificationHub> _hubContext;

        public AthleteCompetitionStatsUpdatedHandler(
            ILogger<AthleteCompetitionStatsUpdatedHandler> logger,
            AppDataContext dataContext,
            IPlayerLineupScorer scorer,
            IHubContext<NotificationHub> hubContext)
        {
            _logger = logger;
            _dataContext = dataContext;
            _scorer = scorer;
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<AthleteCompetitionStatsUpdated> context)
        {
            var msg = context.Message;

            // Tracked load: the scorer mutates these; Lineup.Slots loaded in
            // full so the lineup total sums every slot, not just this one.
            var slots = await _dataContext.PlayerLineupSlots
                .Include(s => s.Lineup)
                    .ThenInclude(l => l.Slots)
                .Where(s => s.ContestId == msg.ContestId &&
                            s.AthleteSeasonId == msg.AthleteSeasonId &&
                            !s.IsScoreFinal)
                .ToListAsync(context.CancellationToken);

            if (slots.Count == 0) return; // nobody rostered this athlete — the common case

            var lineups = await _scorer.ScoreSlotsAsync(
                msg.Sport, slots, finalize: false, context.CancellationToken);
            if (lineups.Count == 0) return;

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
                    },
                    context.CancellationToken);
            }

            _logger.LogInformation(
                "Player lineup scores updated from statline. ContestId={ContestId} AthleteSeasonId={AthleteSeasonId} Slots={Slots} Lineups={Lineups}",
                msg.ContestId, msg.AthleteSeasonId, slots.Count, lineups.Count);
        }
    }
}
