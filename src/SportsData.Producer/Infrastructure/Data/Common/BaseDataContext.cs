using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Extensions;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public abstract class BaseDataContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<AthleteBase> Athletes { get; set; }

        public DbSet<AthleteExternalId> AthleteExternalIds { get; set; }

        public DbSet<AthleteImage> AthleteImages { get; set; }

        public DbSet<AthleteCareerStatistic> AthleteCareerStatistics { get; set; }

        public DbSet<AthleteCareerStatisticCategory> AthleteCareerStatisticCategories { get; set; }

        public DbSet<AthleteCareerStatisticStat> AthleteCareerStatisticStats { get; set; }

        public DbSet<AthleteSeasonStatistic> AthleteSeasonStatistics { get; set; }

        public DbSet<AthleteSeasonStatisticCategory> AthleteSeasonStatisticCategories { get; set; }

        public DbSet<AthleteSeasonStatisticStat> AthleteSeasonStatisticStats { get; set; }

        public DbSet<AthleteStatus> AthleteStatuses { get; set; }

        public DbSet<CompetitionBroadcast> Broadcasts { get; set; }

        public DbSet<Location> Locations { get; set; }

        public DbSet<Season> Seasons { get; set; }

        public DbSet<SeasonPoll> SeasonPolls { get; set; }

        public DbSet<SeasonPollWeek> SeasonPollWeeks { get; set; }

        public DbSet<SeasonPollWeekEntry> SeasonPollWeekEntries { get; set; }

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
            modelBuilder.ApplyConfiguration(new AthleteBase.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new AthleteCareerStatistic.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteCareerStatisticCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteCareerStatisticStat.EntityConfiguration());

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

        /// <summary>
        /// Indexes every ExternalId table on SourceUrlHash — the document
        /// identity-resolution predicate (Provider + SourceUrlHash EXISTS)
        /// that runs for EVERY processed document. Without it those lookups
        /// sequential-scan tables of up to 14M rows; on 2026-08-28 (NCAAFB
        /// kickoff) 80+ concurrent multi-second scans saturated the shared
        /// PG box and queued every other query (UI matchups hit 30s) behind
        /// outbox lock pileups. MUST be called at the END of each concrete
        /// context's OnModelCreating — sport-specific ExternalId entities
        /// aren't in the model yet when the base method runs.
        /// </summary>
        protected static void ApplyExternalIdSourceUrlHashIndexes(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ExternalId).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .HasIndex(nameof(ExternalId.SourceUrlHash));
                }
            }
        }
    }
}
