﻿using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options)
            : base(options) { }

        public DbSet<Franchise> Franchises { get; set; }

        public DbSet<FranchiseExternalId> FranchiseExternalIds { get; set; }

        public DbSet<FranchiseLogo> FranchiseLogos { get; set; }

        public DbSet<FranchiseSeason> FranchiseSeasons { get; set; }

        public DbSet<FranchiseSeasonLogo> FranchiseSeasonLogos { get; set; }

        public DbSet<GroupExternalId> GroupExternalIds { get; set; }

        public DbSet<Group> Groups { get; set; }

        public DbSet<GroupLogo> GroupLogos { get; set; }

        public DbSet<GroupSeason> GroupSeasons { get; set; }

        public DbSet<GroupSeasonLogo> GroupSeasonLogos { get; set; }

        public DbSet<Venue> Venues { get; set; }

        public DbSet<VenueImage> VenueImages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(Venue.EntityConfiguration).Assembly);
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            modelBuilder.Entity<Venue>().Property(x => x.Id).ValueGeneratedNever();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(c => c.Log((RelationalEventId.CommandExecuting, LogLevel.Error)));
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }
}
