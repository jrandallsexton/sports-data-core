using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Eventing.Events.Seasons;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Reacts to <see cref="SeasonPollWeekCreated"/> by re-firing matchup
    /// generation for every active league whose <c>RankingFilter</c> is set
    /// and whose window overlaps the affected <c>SeasonWeek</c>.
    ///
    /// <para>
    /// Without this handler, a TOP25-filtered league created before the
    /// preseason AP poll lands gets its SEC conference matchups at creation
    /// time but never gets the newly-ranked Top 25 contests added — the
    /// <c>AreMatchupsGenerated</c> flag is set by the conference pass, and
    /// the daily <c>MatchupScheduler</c> only re-fires when that flag is
    /// false. The refresh-variant of
    /// <see cref="ScheduleGroupWeekMatchupsCommand"/> bypasses that gate so
    /// the upsert-by-ContestId loop in <see cref="MatchupScheduleProcessor"/>
    /// adds the newly-eligible ranked contests.
    /// </para>
    ///
    /// <para>
    /// Preseason / postseason "headline" polls arrive with
    /// <c>SeasonWeekId = null</c> and are skipped here — they don't map to a
    /// pickable week.
    /// </para>
    /// </summary>
    public class SeasonPollWeekCreatedHandler : IConsumer<SeasonPollWeekCreated>
    {
        private readonly ILogger<SeasonPollWeekCreatedHandler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public SeasonPollWeekCreatedHandler(
            ILogger<SeasonPollWeekCreatedHandler> logger,
            AppDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Consume(ConsumeContext<SeasonPollWeekCreated> context)
        {
            var evt = context.Message;

            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = evt.CorrelationId,
                       ["Sport"] = evt.Sport,
                       ["SeasonWeekId"] = evt.SeasonWeekId ?? Guid.Empty,
                       ["PollSlug"] = evt.PollSlug ?? string.Empty,
                   }))
            {
                if (!evt.SeasonWeekId.HasValue)
                {
                    _logger.LogInformation(
                        "SeasonPollWeekCreated with null SeasonWeekId (preseason/postseason headline poll); nothing to refresh.");
                    return;
                }

                if (!evt.SeasonWeekStartDate.HasValue || !evt.SeasonWeekEndDate.HasValue)
                {
                    // Producer always populates these when SeasonWeekId is set.
                    // Defensive log — if this ever fires, the Producer-side
                    // publish branch is missing the lookup.
                    _logger.LogWarning(
                        "SeasonPollWeekCreated carried SeasonWeekId but no date bounds; cannot overlap-test league windows.");
                    return;
                }

                var seasonWeekId = evt.SeasonWeekId.Value;
                var weekStart = evt.SeasonWeekStartDate.Value;
                var weekEnd = evt.SeasonWeekEndDate.Value;

                // Active leagues that:
                //   • match the event's sport
                //   • use a RankingFilter (the only thing this refresh path
                //     affects — conference-only leagues don't care about new
                //     polls)
                //   • aren't deactivated
                //   • whose window overlaps [weekStart, weekEnd]
                //   • already have a PickemGroupWeek for the affected
                //     SeasonWeekId (no shell → nothing to refresh; the daily
                //     scheduler or creation-time bootstrap will create it
                //     when its own conditions are met)
                var affected = await _dataContext.PickemGroups
                    .AsNoTracking()
                    .Where(g => g.Sport == evt.Sport
                                && g.RankingFilter != null
                                && g.DeactivatedUtc == null
                                && (g.StartsOn == null || g.StartsOn <= weekEnd)
                                && (g.EndsOn == null || g.EndsOn >= weekStart))
                    .Join(
                        _dataContext.PickemGroupWeeks.Where(w => w.SeasonWeekId == seasonWeekId),
                        g => g.Id,
                        w => w.GroupId,
                        (g, w) => new
                        {
                            g.Id,
                            w.SeasonWeek,
                            w.SeasonYear,
                            w.IsNonStandardWeek,
                        })
                    .ToListAsync(context.CancellationToken);

                if (affected.Count == 0)
                {
                    _logger.LogInformation(
                        "SeasonPollWeekCreated for SeasonWeek {SeasonWeekId} affects 0 leagues; nothing to enqueue.",
                        seasonWeekId);
                    return;
                }

                _logger.LogInformation(
                    "SeasonPollWeekCreated for SeasonWeek {SeasonWeekId} affects {Count} league(s); enqueuing refresh.",
                    seasonWeekId, affected.Count);

                foreach (var league in affected)
                {
                    var cmd = new ScheduleGroupWeekMatchupsCommand(
                        league.Id,
                        seasonWeekId,
                        league.SeasonYear,
                        league.SeasonWeek,
                        league.IsNonStandardWeek,
                        evt.CorrelationId,
                        IsRefresh: true);

                    _backgroundJobProvider.Enqueue<IScheduleGroupWeekMatchups>(p => p.Process(cmd));
                }
            }
        }
    }
}
