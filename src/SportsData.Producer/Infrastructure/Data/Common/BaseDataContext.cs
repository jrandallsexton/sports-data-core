using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Extensions;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public abstract class BaseDataContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Athlete> Athletes { get; set; }

        public DbSet<AthleteExternalId> AthleteExternalIds { get; set; }

        public DbSet<AthleteImage> AthleteImages { get; set; }

        public DbSet<AthleteSeasonStatistic> AthleteSeasonStatistics { get; set; }

        public DbSet<AthleteSeasonStatisticCategory> AthleteSeasonStatisticCategories { get; set; }

        public DbSet<AthleteSeasonStatisticStat> AthleteSeasonStatisticStats { get; set; }

        public DbSet<AthleteStatus> AthleteStatuses { get; set; }

        public DbSet<CompetitionBroadcast> Broadcasts { get; set; }

        public DbSet<Location> Locations { get; set; }

        public DbSet<Season> Seasons { get; set; }

        public DbSet<SeasonPoll> SeasonPolls { get; set; }

        public DbSet<SeasonPollWeek> SeasonPollWeeks { get; set; }

        public DbSet<SeasonWeek> SeasonWeeks { get; set; }

        public DbSet<SeasonExternalId> SeasonExternalIds { get; set; }

        public DbSet<SeasonPhase> SeasonPhases { get; set; }

        public DbSet<SeasonPhaseExternalId> SeasonPhaseExternalIds { get; set; }

        public DbSet<Venue> Venues { get; set; }

        public DbSet<VenueExternalId> VenueExternalIds { get; set; }

        public DbSet<VenueImage> VenueImages { get; set; }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<OutboxState> OutboxStates => Set<OutboxState>();

        public DbSet<InboxState> InboxStates => Set<InboxState>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.WithUriConverter();
            modelBuilder.ApplyConfiguration(new Athlete.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new AthleteCompetitionStatistic.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteCompetitionStatisticCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteCompetitionStatisticStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new AthleteImage.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new AthleteSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeasonExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new AthleteSeasonStatistic.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeasonStatisticCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeasonStatisticStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new AthleteStatus.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionBroadcast.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new Location.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new Season.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new SeasonPhase.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonPhaseExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new SeasonPoll.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonPollWeek.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonPollWeekEntry.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonPollWeekEntryStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new SeasonWeek.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonWeekExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new Venue.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new VenueExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new VenueImage.EntityConfiguration());
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
            // TODO: Disable in higher environs
            // Additional info:  https://www.danielmallott.com/posts/entity-framework-core-configuration-options
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }
}
