using Microsoft.EntityFrameworkCore;

using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data
{
    /// <summary>
    /// Notification service's local read/write store. Per the design doc
    /// (docs/architecture/notification-service-events-and-state.md §3),
    /// Notification consumes fat events and projects only the state that
    /// genuinely belongs to it: device tokens, user preferences, scheduled
    /// job ids, and the audit log. It does NOT project User, League,
    /// Membership, Contest, or Pick — those facts ride on the events.
    /// </summary>
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options)
            : base(options) { }

        public DbSet<UserDevice> UserDevices => Set<UserDevice>();

        public DbSet<UserNotificationPreferences> UserNotificationPreferences => Set<UserNotificationPreferences>();

        public DbSet<PendingScheduledJob> PendingScheduledJobs => Set<PendingScheduledJob>();

        public DbSet<NotificationLog> NotificationLog => Set<NotificationLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // One row per (user, device) — token is a stable per-install identifier
            // from FCM; the same token must not double-register for one user.
            modelBuilder.Entity<UserDevice>()
                .HasIndex(d => new { d.UserId, d.FcmToken })
                .IsUnique();

            // One preferences row per user.
            modelBuilder.Entity<UserNotificationPreferences>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            // Quick lookup when an upstream event needs to find the prior
            // Hangfire job to cancel/reschedule.
            modelBuilder.Entity<PendingScheduledJob>()
                .HasIndex(j => new { j.UserId, j.JobKind, j.TargetId });

            // Idempotency key on FCM dispatch: same correlation + user +
            // channel = same logical send, even on RabbitMQ redelivery.
            // Unique so a race between concurrent consumers seeing the same
            // redelivery is caught at the DB layer (DbUpdateException on the
            // losing insert) rather than producing duplicate audit rows /
            // duplicate pushes — the AnyAsync pre-check alone has a read-then-
            // insert race window that this constraint closes.
            modelBuilder.Entity<NotificationLog>()
                .HasIndex(l => new { l.CorrelationId, l.UserId, l.Channel })
                .IsUnique();
        }
    }
}
