using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.PickemGroups
{
    public class PickemGroupCreatedHandler : IConsumer<PickemGroupCreated>
    {
        private readonly ILogger<PickemGroupCreatedHandler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ISeasonClientFactory _seasonClientFactory;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IDateTimeProvider _dateTimeProvider;

        public PickemGroupCreatedHandler(
            ILogger<PickemGroupCreatedHandler> logger,
            AppDataContext dataContext,
            ISeasonClientFactory seasonClientFactory,
            IProvideBackgroundJobs backgroundJobProvider,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _seasonClientFactory = seasonClientFactory;
            _backgroundJobProvider = backgroundJobProvider;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupCreated> context)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = context.Message.CorrelationId
                   }))
            {
                _logger.LogInformation("New pickem group created event received: {@Message}", context.Message);

                await DispatchAsync(context.Message);
            }
        }

        /// <summary>
        /// Route the just-created league to the appropriate bootstrap path based on
        /// its <see cref="PickemGroup.StartsOn"/> relative to now. Without this
        /// dispatch the handler would eagerly create a <c>PickemGroupWeek</c> for
        /// the sport's *current* week regardless of when the league actually starts,
        /// producing an orphan empty row for any future-start league
        /// (the motivating bug — see docs/league-creation-hardening.md).
        /// </summary>
        private async Task DispatchAsync(PickemGroupCreated @event)
        {
            var group = await _dataContext.PickemGroups
                .Include(x => x.Weeks)
                .Where(x => x.Id == @event.GroupId)
                .FirstOrDefaultAsync();

            if (group is null)
            {
                // Permanent failure: a missing group doesn't fix itself on retry.
                // Log and return rather than throw — throwing would land the
                // message in the DLQ for a state that will never become valid.
                _logger.LogError(
                    "PickemGroupCreated received for unknown group {GroupId}; skipping bootstrap.",
                    @event.GroupId);
                return;
            }

            var mode = ResolveBootstrapMode(group);
            switch (mode)
            {
                case BootstrapMode.Future:
                    _logger.LogInformation(
                        "League {GroupId} starts at {StartsOn}; deferring bootstrap to MatchupScheduler.",
                        group.Id, group.StartsOn);
                    return;

                case BootstrapMode.Immediate:
                    await BootstrapImmediateAsync(@event, group);
                    return;

                default:
                    throw new InvalidOperationException($"Unhandled bootstrap mode: {mode}");
            }
        }

        /// <summary>
        /// Current-week bootstrap path: fetch the sport's current
        /// <see cref="CanonicalSeasonWeekDto"/>, ensure a <see cref="PickemGroupWeek"/>
        /// exists for it, and enqueue matchup generation. Unchanged from the
        /// pre-PR-B behavior except for the (1) <see cref="BootstrapMode.Future"/>
        /// gate above and (2) transient-vs-permanent exception split for the
        /// current-week lookup.
        /// </summary>
        private async Task BootstrapImmediateAsync(PickemGroupCreated @event, PickemGroup group)
        {
            // get the current week for the league's sport (NCAA, NFL, or MLB).
            // ESPN provides native week boundaries for all three via SeasonType, so
            // the Producer-side GetCurrentSeasonWeek endpoint works uniformly.
            var weekResult = await _seasonClientFactory.Resolve(group.Sport).GetCurrentSeasonWeek();
            var currentWeek = weekResult.IsSuccess ? weekResult.Value : null;

            if (currentWeek is null)
            {
                // Transient: the current-week endpoint is offline / sport is briefly
                // between seasons. Throw so MassTransit retries the message. If the
                // sport is permanently offseason, the retry policy will eventually
                // DLQ — that's the correct outcome (a human will reschedule via the
                // daily MatchupScheduler once the new season starts).
                _logger.LogError(
                    "Current week could not be resolved for {Sport} (group {GroupId}); will retry.",
                    group.Sport, group.Id);
                throw new InvalidOperationException(
                    $"Current week unavailable for sport {group.Sport}.");
            }

            var groupWeek = group.Weeks.FirstOrDefault(x => x.SeasonWeekId == currentWeek.Id);

            if (groupWeek is null)
            {
                groupWeek = PickemGroupWeekFactory.CreateForCurrentWeek(group, currentWeek);
                group.Weeks.Add(groupWeek);
                await _dataContext.SaveChangesAsync();
            }

            if (!groupWeek.AreMatchupsGenerated)
            {
                var cmd = new ScheduleGroupWeekMatchupsCommand(
                    group.Id,
                    currentWeek.Id,
                    currentWeek.SeasonYear,
                    currentWeek.WeekNumber,
                    currentWeek.IsNonStandardWeek,
                    @event.CorrelationId);

                // kick off a process to create the PickemGroupWeek and matchups for the current week
                _backgroundJobProvider.Enqueue<IScheduleGroupWeekMatchups>(p => p.Process(cmd));
            }
        }

        /// <summary>
        /// Decide whether to bootstrap now or wait for <c>MatchupScheduler</c>.
        /// Null <see cref="PickemGroup.StartsOn"/> = full-season league → Immediate.
        /// StartsOn at-or-before now (validator already rejected the
        /// EndsOn-in-past case) → Immediate. StartsOn strictly future → Future.
        /// </summary>
        private BootstrapMode ResolveBootstrapMode(PickemGroup group)
        {
            if (!group.StartsOn.HasValue) return BootstrapMode.Immediate;
            return group.StartsOn.Value <= _dateTimeProvider.UtcNow()
                ? BootstrapMode.Immediate
                : BootstrapMode.Future;
        }

        private enum BootstrapMode
        {
            Immediate,
            Future,
        }
    }
}
