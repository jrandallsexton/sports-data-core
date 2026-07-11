using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// A user deleted their account (the API anonymized the canonical record and
    /// published <see cref="UserDeleted"/>). Purge the user's entire footprint in
    /// the Notification store — devices, preferences, pick projection, scheduled
    /// jobs, delivery logs, membership projection, and the user projection — so no
    /// further push or reminder can target them.
    ///
    /// <para>Idempotent: purge is by UserId; a redelivery that finds nothing is a
    /// no-op.</para>
    /// </summary>
    public class UserDeletedConsumer : IConsumer<UserDeleted>
    {
        private readonly ILogger<UserDeletedConsumer> _logger;
        private readonly AppDataContext _dataContext;

        public UserDeletedConsumer(
            ILogger<UserDeletedConsumer> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task Consume(ConsumeContext<UserDeleted> context)
        {
            var msg = context.Message;
            var userId = msg.UserId;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["UserId"] = userId
            });

            var ct = context.CancellationToken;

            var devices = await _dataContext.UserDevices.Where(x => x.UserId == userId).ToListAsync(ct);
            var prefs = await _dataContext.UserNotificationPreferences.Where(x => x.UserId == userId).ToListAsync(ct);
            var picks = await _dataContext.UserPicks.Where(x => x.UserId == userId).ToListAsync(ct);
            var jobs = await _dataContext.PendingScheduledJobs.Where(x => x.UserId == userId).ToListAsync(ct);
            var logs = await _dataContext.NotificationLog.Where(x => x.UserId == userId).ToListAsync(ct);
            var memberships = await _dataContext.PickemGroupMembers.Where(x => x.UserId == userId).ToListAsync(ct);
            var user = await _dataContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);

            _dataContext.UserDevices.RemoveRange(devices);
            _dataContext.UserNotificationPreferences.RemoveRange(prefs);
            _dataContext.UserPicks.RemoveRange(picks);
            _dataContext.PendingScheduledJobs.RemoveRange(jobs);
            _dataContext.NotificationLog.RemoveRange(logs);
            _dataContext.PickemGroupMembers.RemoveRange(memberships);
            if (user is not null)
                _dataContext.Users.Remove(user);

            await _dataContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Purged deleted-user footprint. Devices={Devices}, Prefs={Prefs}, Picks={Picks}, Jobs={Jobs}, Logs={Logs}, Memberships={Memberships}, User={UserPurged}",
                devices.Count, prefs.Count, picks.Count, jobs.Count, logs.Count, memberships.Count, user is not null);
        }
    }
}
