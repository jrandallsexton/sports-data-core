using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Upserts the local <see cref="User"/> projection from a backfill snapshot
    /// (or any future per-user data event) published by the API. Idempotent
    /// by design — repeated backfills, MassTransit at-least-once redelivery,
    /// and post-backfill steady-state updates all converge on the same row.
    ///
    /// <para>
    /// No notification side effects here — this consumer's job is purely to
    /// keep the local projection fresh. Whether a notification fires on
    /// user creation / update is the responsibility of a future
    /// steady-state consumer (e.g. <c>UserCreated</c>), not this backfill
    /// path.
    /// </para>
    /// </summary>
    public class UserDataPublishedConsumer : IConsumer<UserDataPublished>
    {
        private readonly ILogger<UserDataPublishedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public UserDataPublishedConsumer(
            ILogger<UserDataPublishedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<UserDataPublished> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["UserId"] = msg.UserId
            });

            var existing = await _dataContext.Users
                .FirstOrDefaultAsync(u => u.Id == msg.UserId, context.CancellationToken);

            var now = _dateTimeProvider.UtcNow();

            if (existing is null)
            {
                _dataContext.Users.Add(new User
                {
                    Id = msg.UserId,
                    DisplayName = msg.DisplayName,
                    Email = msg.Email,
                    Timezone = string.IsNullOrEmpty(msg.Timezone) ? null : msg.Timezone,
                    CreatedUtc = now,
                    CreatedBy = msg.CausationId
                });

                _logger.LogInformation("User projection inserted. UserId={UserId}", msg.UserId);
            }
            else
            {
                existing.DisplayName = msg.DisplayName;
                existing.Email = msg.Email;
                existing.Timezone = string.IsNullOrEmpty(msg.Timezone) ? null : msg.Timezone;
                existing.ModifiedUtc = now;
                existing.ModifiedBy = msg.CausationId;

                _logger.LogInformation("User projection updated. UserId={UserId}", msg.UserId);
            }

            await _dataContext.SaveChangesAsync(context.CancellationToken);
        }
    }
}
