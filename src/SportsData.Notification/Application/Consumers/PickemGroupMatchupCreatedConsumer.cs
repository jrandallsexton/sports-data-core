using MassTransit;

using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// New matchup landed in a league for a given week. Notification's job:
    /// schedule per-user "your pick is due" / "kickoff" reminders for every
    /// member of the league that has opted in.
    ///
    /// <para>
    /// <b>Known fat-payload gap:</b> the current
    /// <see cref="PickemGroupMatchupCreated"/> event only carries
    /// <c>GroupId, ContestId, SeasonYear, Sport</c>. To actually schedule
    /// reminders we also need: contest <c>StartDateUtc</c> (to compute
    /// fire time), team names (for the notification body), and the league's
    /// member list (the fan-out target). All of those would need to be
    /// fattened onto the event before this consumer can do real work —
    /// today it just logs and exits. See design doc §4 (this event isn't
    /// in the v1–v4 catalog, so the canonical path for kickoff reminders
    /// is via <see cref="ContestStartTimeUpdatedConsumer"/> reacting to
    /// per-sport Producer events, not via this consumer).
    /// </para>
    /// </summary>
    public class PickemGroupMatchupCreatedConsumer : IConsumer<PickemGroupMatchupCreated>
    {
        private readonly ILogger<PickemGroupMatchupCreatedConsumer> _logger;
        private readonly AppDataContext _dataContext;

        public PickemGroupMatchupCreatedConsumer(
            ILogger<PickemGroupMatchupCreatedConsumer> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public Task Consume(ConsumeContext<PickemGroupMatchupCreated> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["GroupId"] = msg.GroupId,
                ["ContestId"] = msg.ContestId,
                ["Sport"] = msg.Sport
            });

            _logger.LogInformation(
                "PickemGroupMatchupCreated received; reminder fan-out deferred pending fat-payload work.");

            // TODO (fat-payload work): fatten this event with StartDateUtc +
            // team names + league member list. Once available:
            //   1. For each member with KickoffReminderEnabled = true and a
            //      UserDevice with NotificationsEnabled = true,
            //   2. Compute fireTime = StartDateUtc - leadMinutes (config-driven)
            //   3. Enqueue Hangfire job: _backgroundJobProvider.Schedule<INotificationDispatcher>(...)
            //   4. Insert PendingScheduledJob row (JobKind="Kickoff",
            //      TargetId=ContestId, HangfireJobId=<returned>)
            //   5. SaveChanges

            return Task.CompletedTask;
        }
    }
}
