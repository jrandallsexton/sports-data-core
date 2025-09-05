using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;

using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public abstract class TeamSportDataContext : BaseDataContext
    {
        protected TeamSportDataContext(DbContextOptions options)
            : base(options) { }

        public DbSet<AthletePosition> AthletePositions { get; set; }
        public DbSet<AthletePositionExternalId> AthletePositionExternalIds { get; set; }

        public DbSet<AthleteSeason> AthleteSeasons { get; set; }
        public DbSet<AthleteSeasonExternalId> AthleteSeasonExternalIds { get; set; }

        public DbSet<Award> Awards { get; set; }
        public DbSet<AwardExternalId> AwardExternalIds { get; set; }

        public DbSet<Coach> Coaches { get; set; }
        public DbSet<CoachExternalId> CoachExternalIds { get; set; }

        public DbSet<CoachRecord> CoachRecords { get; set; }

        public DbSet<CoachRecordStat> CoachRecordStats { get; set; }

        public DbSet<CoachSeason> CoachSeasons { get; set; }

        public DbSet<Competition> Competitions { get; set; }

        public DbSet<CompetitionCompetitor> CompetitionCompetitors { get; set; }

        public DbSet<CompetitionCompetitorLineScore> CompetitionCompetitorLineScores { get; set; }

        public DbSet<CompetitionCompetitorLineScoreExternalId> CompetitionCompetitorLineScoreExternalIds { get; set; }

        public DbSet<CompetitionCompetitorExternalId> CompetitionCompetitorExternalIds { get; set; }

        public DbSet<CompetitionCompetitorScore> CompetitionCompetitorScores { get; set; }

        public DbSet<CompetitionCompetitorScoreExternalId> CompetitionCompetitorScoreExternalIds { get; set; }

        public DbSet<CompetitionExternalId> CompetitionExternalIds { get; set; }

        public DbSet<CompetitionLeader> CompetitionLeaders { get; set; }

        public DbSet<CompetitionOdds> CompetitionOdds { get; set; }

        public DbSet<CompetitionOddsExternalId> CompetitionOddsExternalIds { get; set; }

        public DbSet<CompetitionLeaderStat> CompetitionLeaderStats { get; set; }

        public DbSet<CompetitionLeaderCategory> LeaderCategories { get; set; }

        public DbSet<CompetitionPowerIndex> CompetitionPowerIndexes { get; set; }

        public DbSet<CompetitionPowerIndexExternalId> CompetitionPowerIndexExternalIds { get; set; }

        public DbSet<CompetitionPrediction> CompetitionPredictions { get; set; }

        public DbSet<CompetitionPredictionValue> CompetitionPredictionValues { get; set; }

        public DbSet<CompetitionProbability> CompetitionProbabilities { get; set; }

        public DbSet<CompetitionStatus> CompetitionStatuses { get; set; }

        public DbSet<CompetitionStatusExternalId> CompetitionStatusExternalIds { get; set; }

        public DbSet<Contest> Contests { get; set; }
        public DbSet<ContestExternalId> ContestExternalIds { get; set; }
        

        public DbSet<Drive> Drives { get; set; }
        public DbSet<DriveExternalId> DriveExternalIds { get; set; }

        public DbSet<Franchise> Franchises { get; set; }
        public DbSet<FranchiseExternalId> FranchiseExternalIds { get; set; }

        public DbSet<FranchiseLogo> FranchiseLogos { get; set; }

        public DbSet<FranchiseSeason> FranchiseSeasons { get; set; }

        public DbSet<FranchiseSeasonAward> FranchiseSeasonAwards { get; set; }

        public DbSet<FranchiseSeasonAwardWinner> FranchiseSeasonAwardWinners { get; set; }

        public DbSet<FranchiseSeasonExternalId> FranchiseSeasonExternalIds { get; set; }

        public DbSet<FranchiseSeasonLogo> FranchiseSeasonLogos { get; set; }

        public DbSet<FranchiseSeasonProjection> FranchiseSeasonProjections { get; set; }

        public DbSet<FranchiseSeasonRanking> FranchiseSeasonRankings { get; set; }

        public DbSet<FranchiseSeasonRecord> FranchiseSeasonRecords { get; set; }

        public DbSet<FranchiseSeasonRecordAts> FranchiseSeasonRecordsAts { get; set; }

        public DbSet<FranchiseSeasonRecordAtsCategory> FranchiseSeasonRecordAtsCategories { get; set; }

        public DbSet<FranchiseSeasonStatisticCategory> FranchiseSeasonStatistics { get; set; }

        public DbSet<GroupSeason> GroupSeasons { get; set; }

        public DbSet<GroupSeasonLogo> GroupSeasonLogos { get; set; }

        public DbSet<Play> Plays { get; set; }
        public DbSet<PlayExternalId> PlayExternalIds { get; set; }

        public DbSet<PlayTypeCategory> PlayTypeCategories { get; set; }

        public DbSet<PowerIndex> PowerIndexes { get; set; }

        public DbSet<PredictionMetric> PredictionMetrics { get; set; }

        public DbSet<SeasonFuture> SeasonFutures { get; set; }

        public DbSet<SeasonFutureExternalId> SeasonFutureExternalIds { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new AthletePosition.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthletePositionExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteSeasonExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Award.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AwardExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Coach.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachRecord.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachRecordStat.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Competition.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionCompetitor.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionCompetitorLineScore.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionCompetitorLineScoreExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionLeader.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionLeaderCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionLeaderStat.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionOdds.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionOddsExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionPowerIndex.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionPowerIndexExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionPrediction.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionPredictionValue.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionProbability.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionStatus.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionStatusExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Contest.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new ContestExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Drive.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new DriveExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Franchise.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonAward.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonAwardWinner.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonProjection.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRanking.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecord.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecordAts.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecordAtsCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonRecordStat.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonStatistic.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonStatisticCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupSeasonExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new GroupSeasonLogo.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new Play.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new PlayExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new PlayTypeCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new PowerIndex.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new PredictionMetric.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonFuture.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonFutureExternalId.EntityConfiguration());
        }
    }
}
