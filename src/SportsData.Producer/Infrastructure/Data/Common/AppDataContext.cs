﻿//using MassTransit;

//using Microsoft.EntityFrameworkCore;

//using SportsData.Producer.Infrastructure.Data.Entities;

//namespace SportsData.Producer.Infrastructure.Data.Common
//{
//    public class AppDataContext : DbContext
//    {
//        public AppDataContext(DbContextOptions<AppDataContext> options)
//            : base(options) { }

//        public DbSet<Athlete> Athletes { get; set; }

//        public DbSet<AthleteExternalId> AthleteExternalIds { get; set; }

//        public DbSet<AthleteImage> AthleteImages { get; set; }

//        public DbSet<Franchise> Franchises { get; set; }

//        public DbSet<FranchiseExternalId> FranchiseExternalIds { get; set; }

//        public DbSet<FranchiseLogo> FranchiseLogos { get; set; }

//        public DbSet<FranchiseSeason> FranchiseSeasons { get; set; }

//        public DbSet<FranchiseSeasonLogo> FranchiseSeasonLogos { get; set; }

//        public DbSet<GroupExternalId> GroupExternalIds { get; set; }

//        public DbSet<Group> Groups { get; set; }

//        public DbSet<GroupLogo> GroupLogos { get; set; }

//        public DbSet<GroupSeason> GroupSeasons { get; set; }

//        public DbSet<GroupSeasonLogo> GroupSeasonLogos { get; set; }

//        public DbSet<Position> Positions { get; set; }

//        protected override void OnModelCreating(ModelBuilder modelBuilder)
//        {
//            base.OnModelCreating(modelBuilder);
//            //modelBuilder.ApplyConfigurationsFromAssembly(typeof(Venue.EntityConfiguration).Assembly);
//            modelBuilder.ApplyConfiguration(new Athlete.EntityConfiguration());
//            modelBuilder.AddInboxStateEntity();
//            modelBuilder.AddOutboxStateEntity();
//            modelBuilder.AddOutboxMessageEntity();
//        }

//        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//        {
//            // TODO: Disable in higher environs
//            // Additional info:  https://www.danielmallott.com/posts/entity-framework-core-configuration-options
//            optionsBuilder.EnableSensitiveDataLogging();
//            optionsBuilder.EnableDetailedErrors();
//        }
//    }
//}
