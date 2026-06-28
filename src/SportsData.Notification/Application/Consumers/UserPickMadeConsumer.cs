using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Projects an active user pick (from the API's <c>UserPickMade</c> event)
    /// into the local <see cref="UserPick"/> table, so contest-level events —
    /// e.g. <c>ContestOddsUpdated</c> — can be targeted at users who actually
    /// picked the contest rather than all league members.
    ///
    /// <para>
    /// Idempotent on the <c>(UserId, ContestId, PickemGroupId)</c> natural key:
    /// re-submitting / changing a pick republishes the same key, and the row
    /// only records that the pick exists (the picked side isn't needed for
    /// line-move fan-out), so an existing row is a no-op. Race-safe insert — a
    /// concurrent consumer losing the unique-constraint insert falls through to
    /// the no-op path.
    /// </para>
    /// </summary>
    public class UserPickMadeConsumer : IConsumer<UserPickMade>
    {
        private readonly ILogger<UserPickMadeConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public UserPickMadeConsumer(
            ILogger<UserPickMadeConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<UserPickMade> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["UserId"] = msg.UserId,
                ["ContestId"] = msg.ContestId
            });

            var exists = await _dataContext.UserPicks
                .AnyAsync(
                    p => p.UserId == msg.UserId &&
                         p.ContestId == msg.ContestId &&
                         p.PickemGroupId == msg.PickemGroupId,
                    context.CancellationToken);

            if (exists)
            {
                _logger.LogInformation("UserPick already projected; no-op. UserId={UserId}, ContestId={ContestId}", msg.UserId, msg.ContestId);
                return;
            }

            var now = _dateTimeProvider.UtcNow();
            _dataContext.UserPicks.Add(new UserPick
            {
                Id = Guid.NewGuid(),
                UserId = msg.UserId,
                ContestId = msg.ContestId,
                PickemGroupId = msg.PickemGroupId,
                CreatedUtc = now,
                CreatedBy = msg.CausationId
            });

            try
            {
                await _dataContext.SaveChangesAsync(context.CancellationToken);
                _logger.LogInformation("UserPick projected. UserId={UserId}, ContestId={ContestId}", msg.UserId, msg.ContestId);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Race: another consumer inserted the same (UserId, ContestId,
                // PickemGroupId) first. The pick is projected either way — no-op.
                _logger.LogInformation("UserPick already projected (insert race); no-op. UserId={UserId}, ContestId={ContestId}", msg.UserId, msg.ContestId);
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
