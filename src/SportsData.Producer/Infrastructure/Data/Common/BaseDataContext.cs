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

        public DbSet<AthleteStatus> AthleteStatuses { get; set; }

        public DbSet<Broadcast> Broadcasts { get; set; }

        public DbSet<Location> Locations { get; set; }

        public DbSet<Season> Seasons { get; set; }

        public DbSet<SeasonExternalId> SeasonExternalIds { get; set; }

        public DbSet<SeasonPhase> SeasonPhases { get; set; }

        public DbSet<SeasonPhaseExternalId> SeasonPhaseExternalIds { get; set; }

        public DbSet<Venue> Venues { get; set; }

        public DbSet<VenueExternalId> VenueExternalIds { get; set; }

        public DbSet<VenueImage> VenueImages { get; set; }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<OutboxState> OutboxStates => Set<OutboxState>();

        public DbSet<InboxState> InboxStates => Set<InboxState>();

        public DbSet<OutboxPing> OutboxPings => Set<OutboxPing>();


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.WithUriConverter();
            modelBuilder.ApplyConfiguration(new Athlete.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteImage.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteStatus.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Broadcast.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Location.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Season.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonPhase.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonPhaseExternalId.EntityConfiguration());
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
