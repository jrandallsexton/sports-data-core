using MassTransit;

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

        public DbSet<Location> Locations { get; set; }

        public DbSet<Season> Seasons { get; set; }

        public DbSet<Venue> Venues { get; set; }

        public DbSet<VenueImage> VenueImages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.WithUriConverter();
            modelBuilder.ApplyConfiguration(new Athlete.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteImage.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteStatus.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Location.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Season.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Venue.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new VenueImage.EntityConfiguration());
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            //modelBuilder.Entity<Venue>().UseTpcMappingStrategy();
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
