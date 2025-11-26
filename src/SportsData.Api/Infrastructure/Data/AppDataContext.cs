using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data;

public class AppDataContext : DbContext
{
    public AppDataContext(DbContextOptions<AppDataContext> options)
        : base(options) { }

    public DbSet<User> Users { get; set; }

    public DbSet<ContestPrediction> ContestPredictions { get; set; }

    public DbSet<PickemGroup> PickemGroups { get; set; }

    public DbSet<PickemGroupWeek> PickemGroupWeeks { get; set; }

    public DbSet<PickemGroupMatchup> PickemGroupMatchups { get; set; }

    public DbSet<PickemGroupMember> PickemGroupMembers { get; set; }

    public DbSet<PickemGroupUserPick> UserPicks { get; set; }

    public DbSet<PickResult> PickResults { get; set; }

    public DbSet<PickemGroupWeekResult> PickemGroupWeekResults { get; set; }

    public DbSet<MatchupPreview> MatchupPreviews { get; set; }

    public DbSet<Article> Articles { get; set; }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<OutboxState> OutboxStates => Set<OutboxState>();

    public DbSet<InboxState> InboxStates => Set<InboxState>();

    public DbSet<OutboxPing> OutboxPings => Set<OutboxPing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Entities.User.EntityConfiguration).Assembly);

        // Seed the Firebase test user
        var seedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<User>().HasData(new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FirebaseUid = "ngovRAr5E8cjMVaZNvcqN1nPFPJ2",
            Email = "foo@bar.com",
            EmailVerified = true,
            SignInProvider = "password",
            CreatedUtc = seedTime,
            LastLoginUtc = seedTime,
            DisplayName = "sportDeets"
        });

        modelBuilder.AddInboxStateEntity(cfg =>
        {
            cfg.ToTable(nameof(InboxState));
        });

        modelBuilder.AddOutboxStateEntity(cfg =>
        {
            cfg.ToTable(nameof(OutboxState));
        });

        modelBuilder.AddOutboxMessageEntity(cfg =>
        {
            cfg.ToTable(nameof(OutboxMessage));
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(x =>
        {
            x.Ignore([RelationalEventId.PendingModelChangesWarning]);
        });
        optionsBuilder.EnableSensitiveDataLogging(false);
    }
}
