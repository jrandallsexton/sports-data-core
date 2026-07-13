using Microsoft.EntityFrameworkCore;

using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Data
{
    /// <summary>
    /// Notification service's local read/write store. The original design
    /// doc (docs/architecture/notification-service-events-and-state.md §3)
    /// kept this to 4 tables — device tokens, user preferences, scheduled
    /// job ids, audit log — and pushed everything else onto fat events.
    ///
    /// <para>
    /// The <c>User</c>, <c>PickemGroup</c>, and <c>PickemGroupMember</c>
    /// projections are deliberate expansions of that model for operational
    /// queries: the backfill chains (<c>UsersRequested</c>/<c>UserDataPublished</c>
    /// and <c>PickemGroupsRequested</c>/<c>PickemGroupDataPublished</c>) seed
    /// them, and league-wide fan-out flows that need to look up entities
    /// without round-tripping to API will read from them.
    /// </para>
    /// </summary>
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options)
            : base(options) { }

        public DbSet<PickemGroup> PickemGroups => Set<PickemGroup>();

        public DbSet<PickemGroupMatchup> PickemGroupMatchups => Set<PickemGroupMatchup>();

        public DbSet<PickemGroupMember> PickemGroupMembers => Set<PickemGroupMember>();

        public DbSet<User> Users => Set<User>();

        public DbSet<UserDevice> UserDevices => Set<UserDevice>();

        public DbSet<UserPick> UserPicks => Set<UserPick>();

        public DbSet<UserNotificationPreferences> UserNotificationPreferences => Set<UserNotificationPreferences>();

        public DbSet<PendingScheduledJob> PendingScheduledJobs => Set<PendingScheduledJob>();

        public DbSet<NotificationLog> NotificationLog => Set<NotificationLog>();

        public DbSet<NotificationUserPick> NotificationUserPicks => Set<NotificationUserPick>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // One row per physical install. InstallationId is the device's stable
            // identity, so a device has exactly one current owner; re-registration
            // by a different user reassigns the row (UserId + FcmToken) rather than
            // creating a second row pointing at the same FCM token.
            modelBuilder.Entity<UserDevice>()
                .HasIndex(d => d.InstallationId)
                .IsUnique();

            // The dominant read is the send flow ("all of user U's devices"):
            // NotificationDispatcher and the per-event consumers all filter
            // WHERE UserId == u && NotificationsEnabled. The unique InstallationId
            // index doesn't serve that, so keep a UserId-prefixed access path
            // (previously provided by the old (UserId, FcmToken) unique index).
            modelBuilder.Entity<UserDevice>()
                .HasIndex(d => d.UserId);

            // One pick row per (user, contest, league); the consumer upserts on
            // it. The dominant read is "who picked contest C" (odds/line-move
            // fan-out), so ContestId leads a second index.
            modelBuilder.Entity<UserPick>()
                .HasIndex(p => new { p.UserId, p.ContestId, p.PickemGroupId })
                .IsUnique();

            modelBuilder.Entity<UserPick>()
                .HasIndex(p => p.ContestId);

            // One preferences row per user.
            modelBuilder.Entity<UserNotificationPreferences>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            // Natural-key lookup when an upstream event needs to find the
            // prior Hangfire job to cancel/reschedule. SeasonWeek included
            // because PickDeadline rows are scoped per league per week —
            // omitting it would collide when a league has matchups generated
            // multiple weeks ahead. Null SeasonWeek (ContestStart jobs) still
            // participates in the index per Postgres semantics.
            //
            // Unique because the natural-key invariant is "one scheduled row
            // per user per scope" — two consumer runs racing to schedule the
            // same reminder would otherwise persist duplicates and break the
            // scheduler's ToDictionaryAsync lookup. Race losers catch
            // DbUpdateException(23505) and fall through to the reschedule
            // path against the winner's row.
            //
            // AreNullsDistinct(false): ContestStart jobs leave SeasonWeek
            // null, and Postgres treats nulls as distinct by default. Without
            // this, duplicate ContestStart rows for the same
            // (User, "ContestStart", ContestId) tuple would slip through the
            // unique index. Requires Postgres 15+ (the live cluster runs 16,
            // so we're fine).
            modelBuilder.Entity<PendingScheduledJob>()
                .HasIndex(j => new { j.UserId, j.JobKind, j.TargetId, j.SeasonWeek })
                .IsUnique()
                .AreNullsDistinct(false);

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

            // Typed pick-result idempotency key: one push per pick, ever. A pick
            // is scored exactly once, so (UserId, PickId) both stops the cross-run
            // duplicate the old CorrelationId-based key let through AND preserves
            // the per-league notifications it wrongly collapsed (a user in N
            // leagues who picked one game has N distinct PickIds). CorrelationId
            // is a tracing column here, not part of the key. Replaces the
            // NotificationLog claim for the PickResult path.
            // See docs/architecture/notification-log-table-per-type.md.
            modelBuilder.Entity<NotificationUserPick>()
                .HasIndex(l => new { l.UserId, l.PickId })
                .IsUnique();

            // League-wide fan-out is the dominant query against this join
            // ("for every member of league X..."), so index on GroupId.
            // Unique (PickemGroupId, UserId) catches duplicate memberships at
            // the DB layer if a backfill ever races itself.
            modelBuilder.Entity<PickemGroupMember>()
                .HasIndex(m => new { m.PickemGroupId, m.UserId })
                .IsUnique();

            // Matchup uniqueness key — same contest can appear in many
            // leagues, but no league should ever have the same contest
            // twice. Lookup queries also hit by ContestId alone (e.g.,
            // ContestStartTimeUpdated → update every matchup row for the
            // contest) so the leading column is PickemGroupId for fan-out
            // ("which contests are in this league this week") and a
            // separate non-unique index on ContestId carries the per-
            // contest update path.
            modelBuilder.Entity<PickemGroupMatchup>()
                .HasIndex(m => new { m.PickemGroupId, m.ContestId })
                .IsUnique();
            modelBuilder.Entity<PickemGroupMatchup>()
                .HasIndex(m => m.ContestId);
        }
    }
}
