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
    /// Two specific idempotency properties:
    /// <list type="bullet">
    ///   <item><b>Race-safe insert.</b> Two concurrent consumers seeing
    ///   "doesn't exist" can both try to insert. The PK-unique constraint
    ///   catches the loser via <see cref="DbUpdateException"/>; we detach
    ///   the orphan entity and fall through to the update path.</item>
    ///   <item><b>No-op redelivery.</b> If the snapshot's business fields
    ///   match what's already in the projection, we don't touch
    ///   <c>ModifiedUtc</c>/<c>ModifiedBy</c> and skip the SaveChanges
    ///   round-trip entirely.</item>
    /// </list>
    /// </para>
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

            var normalizedTimezone = string.IsNullOrEmpty(msg.Timezone) ? null : msg.Timezone;
            var now = _dateTimeProvider.UtcNow();

            var existing = await _dataContext.Users
                .FirstOrDefaultAsync(u => u.Id == msg.UserId, context.CancellationToken);

            if (existing is null)
            {
                var entity = new User
                {
                    Id = msg.UserId,
                    DisplayName = msg.DisplayName,
                    Email = msg.Email,
                    Timezone = normalizedTimezone,
                    CreatedUtc = now,
                    CreatedBy = msg.CausationId
                };
                _dataContext.Users.Add(entity);

                try
                {
                    await _dataContext.SaveChangesAsync(context.CancellationToken);
                    _logger.LogInformation("User projection inserted. UserId={UserId}", msg.UserId);
                    return;
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Race: another consumer inserted first. Detach our orphan
                    // and fall through to the update path.
                    _dataContext.Entry(entity).State = EntityState.Detached;
                    existing = await _dataContext.Users
                        .FirstAsync(u => u.Id == msg.UserId, context.CancellationToken);
                }
            }

            // Update path — only touch Modified* if a business field actually
            // changed. Repeated identical deliveries leave the row stable.
            var changed =
                existing.DisplayName != msg.DisplayName
                || existing.Email != msg.Email
                || existing.Timezone != normalizedTimezone;

            if (!changed)
            {
                _logger.LogDebug("User projection unchanged. UserId={UserId}", msg.UserId);
                return;
            }

            existing.DisplayName = msg.DisplayName;
            existing.Email = msg.Email;
            existing.Timezone = normalizedTimezone;
            existing.ModifiedUtc = now;
            existing.ModifiedBy = msg.CausationId;

            await _dataContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("User projection updated. UserId={UserId}", msg.UserId);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
