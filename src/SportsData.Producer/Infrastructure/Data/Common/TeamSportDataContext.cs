using MassTransit.AzureServiceBusTransport.Topology;
using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public abstract class TeamSportDataContext(DbContextOptions options) :
        BaseDataContext(options)
    {
        public DbSet<AthletePosition> AthletePositions { get; set; }

        public DbSet<AthletePositionExternalId> AthletePositionExternalIds { get; set; }

        public DbSet<Award> Awards { get; set; }

        public DbSet<AwardExternalId> AwardExternalIds { get; set; }

        public DbSet<Coach> Coaches { get; set; }

        public DbSet<CoachSeason> CoachSeasons { get; set; }

        public DbSet<CoachExternalId> CoachExternalIds { get; set; }

        public DbSet<Contest> Contests { get; set; }

        public DbSet<Competition> Competitions { get; set; }

        public DbSet<CompetitionExternalId> CompetitionExternalIds { get; set; }

        public DbSet<Drive> Drives { get; set; }

        public DbSet<Play> Plays { get; set; }

        public DbSet<ContestExternalId> ContestExternalIds { get; set; }

        public DbSet<ContestOdds> ContestOdds { get; set; }

        public DbSet<Franchise> Franchises { get; set; }

        public DbSet<FranchiseExternalId> FranchiseExternalIds { get; set; }

        public DbSet<FranchiseLogo> FranchiseLogos { get; set; }

        public DbSet<FranchiseSeason> FranchiseSeasons { get; set; }

        public DbSet<FranchiseSeasonExternalId> FranchiseSeasonExternalIds { get; set; }

        public DbSet<FranchiseSeasonAward> FranchiseSeasonAwards { get; set; }

        public DbSet<FranchiseSeasonAwardWinner> FranchiseSeasonAwardWinners { get; set; }

        public DbSet<FranchiseSeasonLogo> FranchiseSeasonLogos { get; set; }

        public DbSet<FranchiseSeasonProjection> FranchiseSeasonProjections { get; set; }

        public DbSet<FranchiseSeasonRecord> FranchiseSeasonRecords { get; set; }

        public DbSet<FranchiseSeasonRecordAts> FranchiseSeasonRecordsAts { get; set; }

        public DbSet<FranchiseSeasonStatisticCategory> FranchiseSeasonStatistics { get; set; }

        public DbSet<Group> Groups { get; set; }

        public DbSet<GroupExternalId> GroupExternalIds { get; set; }

        public DbSet<GroupLogo> GroupLogos { get; set; }

        public DbSet<GroupSeason> GroupSeasons { get; set; }

        public DbSet<GroupSeasonLogo> GroupSeasonLogos { get; set; }

        public DbSet<SeasonFuture> SeasonFutures { get; set; }

        public DbSet<SeasonFutureExternalId> SeasonFutureExternalIds { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // TODO: See about registering these dynamically based on the context type
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new AthletePosition.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Award.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AwardExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Coach.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Contest.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Competition.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Drive.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Play.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new ContestExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new ContestOdds.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Franchise.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonAward.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonAwardWinner.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonProjection.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecord.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecordAts.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecordAtsCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecordStat.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonStatistic.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonStatisticCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Group.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupSeasonLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonFuture.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonFutureExternalId.EntityConfiguration());
        }
    }
}
