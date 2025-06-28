using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public abstract class TeamSportDataContext(DbContextOptions options) :
        BaseDataContext(options)
    {
        public DbSet<AthletePosition> AthletePositions { get; set; }

        public DbSet<Contest> Contests { get; set; }

        public DbSet<ContestOdds> ContestOdds { get; set; }

        public DbSet<Franchise> Franchises { get; set; }

        public DbSet<FranchiseExternalId> FranchiseExternalIds { get; set; }

        public DbSet<FranchiseLogo> FranchiseLogos { get; set; }

        public DbSet<FranchiseSeason> FranchiseSeasons { get; set; }

        public DbSet<FranchiseSeasonLogo> FranchiseSeasonLogos { get; set; }

        public DbSet<FranchiseSeasonRecord> FranchiseSeasonRecords { get; set; }

        public DbSet<Group> Groups { get; set; }

        public DbSet<GroupExternalId> GroupExternalIds { get; set; }

        public DbSet<GroupLogo> GroupLogos { get; set; }

        public DbSet<GroupSeason> GroupSeasons { get; set; }

        public DbSet<GroupSeasonLogo> GroupSeasonLogos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // TODO: See about registering these dynamically based on the context type
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new AthletePosition.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Contest.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new ContestOdds.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Franchise.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecord.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecordStat.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Group.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupSeasonLogo.EntityConfiguration());
        }
    }
}
