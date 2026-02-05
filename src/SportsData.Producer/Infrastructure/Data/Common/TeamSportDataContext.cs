using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

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

        public DbSet<AthleteSeasonInjury> AthleteSeasonInjuries { get; set; }

        public DbSet<AthleteSeasonNote> AthleteSeasonNotes { get; set; }

        public DbSet<AthleteCompetition> AthleteCompetitions { get; set; }

        public DbSet<AthleteCompetitionStatistic> AthleteCompetitionStatistics { get; set; }
        public DbSet<AthleteCompetitionStatisticCategory> AthleteCompetitionStatisticCategories { get; set; }
        public DbSet<AthleteCompetitionStatisticStat> AthleteCompetitionStatisticStats { get; set; }

        public DbSet<Award> Awards { get; set; }
        public DbSet<AwardExternalId> AwardExternalIds { get; set; }

        public DbSet<Coach> Coaches { get; set; }
        public DbSet<CoachExternalId> CoachExternalIds { get; set; }

        public DbSet<CoachRecord> CoachRecords { get; set; }

        public DbSet<CoachRecordStat> CoachRecordStats { get; set; }

        public DbSet<CoachSeason> CoachSeasons { get; set; }

        public DbSet<CoachSeasonRecord> CoachSeasonRecords { get; set; }

        public DbSet<CoachSeasonRecordStat> CoachSeasonRecordStats { get; set; }

        public DbSet<CompetitionMetric> CompetitionMetrics { get; set; }

        public DbSet<CompetitionMedia> CompetitionMedia { get; set; }

        public DbSet<CompetitionStream> CompetitionStreams { get; set; }

        public DbSet<Competition> Competitions { get; set; }

        public DbSet<CompetitionCompetitor> CompetitionCompetitors { get; set; }

        public DbSet<CompetitionCompetitorLineScore> CompetitionCompetitorLineScores { get; set; }

        public DbSet<CompetitionCompetitorLineScoreExternalId> CompetitionCompetitorLineScoreExternalIds { get; set; }

        public DbSet<CompetitionCompetitorExternalId> CompetitionCompetitorExternalIds { get; set; }

        public DbSet<CompetitionCompetitorScore> CompetitionCompetitorScores { get; set; }

        public DbSet<CompetitionCompetitorScoreExternalId> CompetitionCompetitorScoreExternalIds { get; set; }

        public DbSet<CompetitionCompetitorStatistic> CompetitionCompetitorStatistics { get; set; }
        public DbSet<CompetitionCompetitorStatisticCategory> CompetitionCompetitorStatisticCategories { get; set; }
        public DbSet<CompetitionCompetitorStatisticStat> CompetitionCompetitorStatisticStats { get; set; }

        public DbSet<CompetitionCompetitorRecord> CompetitionCompetitorRecords { get; set; }
        public DbSet<CompetitionCompetitorRecordStat> CompetitionCompetitorRecordStats { get; set; }

        public DbSet<CompetitionExternalId> CompetitionExternalIds { get; set; }

        public DbSet<CompetitionLeaderCategory> CompetitionLeaderCategories { get; set; }

        public DbSet<CompetitionLeader> CompetitionLeaders { get; set; }

        public DbSet<CompetitionOdds> CompetitionOdds { get; set; }
        public DbSet<CompetitionOddsExternalId> CompetitionOddsExternalIds { get; set; }
        public DbSet<CompetitionTeamOdds> CompetitionTeamOdds { get; set; }
        public DbSet<CompetitionOddsLink> CompetitionOddsLinks { get; set; }

        public DbSet<CompetitionLeaderStat> CompetitionLeaderStats { get; set; }

        public DbSet<CompetitionLeaderCategory> LeaderCategories { get; set; }

        public DbSet<CompetitionPowerIndex> CompetitionPowerIndexes { get; set; }

        public DbSet<CompetitionPowerIndexExternalId> CompetitionPowerIndexExternalIds { get; set; }

        public DbSet<CompetitionPrediction> CompetitionPredictions { get; set; }

        public DbSet<CompetitionPredictionValue> CompetitionPredictionValues { get; set; }

        public DbSet<CompetitionProbability> CompetitionProbabilities { get; set; }

        public DbSet<CompetitionSituation> CompetitionSituations { get; set; }

        public DbSet<CompetitionStatus> CompetitionStatuses { get; set; }

        public DbSet<CompetitionStatusExternalId> CompetitionStatusExternalIds { get; set; }

        public DbSet<Contest> Contests { get; set; }
        public DbSet<ContestExternalId> ContestExternalIds { get; set; }

        public DbSet<CompetitionDrive> Drives { get; set; }
        public DbSet<CompetitionDriveExternalId> DriveExternalIds { get; set; }

        public DbSet<Franchise> Franchises { get; set; }
        public DbSet<FranchiseExternalId> FranchiseExternalIds { get; set; }

        public DbSet<FranchiseLogo> FranchiseLogos { get; set; }

        public DbSet<FranchiseSeasonMetric> FranchiseSeasonMetrics { get; set; }

        public DbSet<FranchiseSeason> FranchiseSeasons { get; set; }

        public DbSet<FranchiseSeasonAward> FranchiseSeasonAwards { get; set; }

        public DbSet<FranchiseSeasonAwardWinner> FranchiseSeasonAwardWinners { get; set; }

        public DbSet<FranchiseSeasonLeader> FranchiseSeasonLeaders { get; set; }

        public DbSet<FranchiseSeasonLeaderStat> FranchiseSeasonLeaderStats { get; set; }

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

        public DbSet<CompetitionPlay> CompetitionPlays { get; set; }
        public DbSet<CompetitionPlayExternalId> CompetitionPlayExternalIds { get; set; }

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

            modelBuilder.ApplyConfiguration(new AthleteSeasonInjury.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new AthleteCompetition.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new AthleteCompetitionStatistic.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteCompetitionStatisticCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AthleteCompetitionStatisticStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new Award.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new AwardExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new Coach.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CoachRecord.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachRecordStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CoachSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachSeasonRecord.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CoachSeasonRecordStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new Competition.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionCompetitor.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionCompetitorLineScore.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionCompetitorLineScoreExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionCompetitorStatistic.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionCompetitorStatisticCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionCompetitorStatisticStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionCompetitorRecord.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionCompetitorRecordStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionLeader.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionLeaderCategory.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionLeaderStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionMedia.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionMetric.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionOdds.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionOddsExternalId.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionTeamOdds.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionOddsLink.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionPowerIndex.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionPowerIndexExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionPrediction.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionPredictionValue.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionProbability.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionSituation.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionStatus.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionStatusExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionStream.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new Contest.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new ContestExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new CompetitionDrive.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionDriveExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new Franchise.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new FranchiseLogo.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new FranchiseSeason.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new FranchiseSeasonAward.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonAwardWinner.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new FranchiseSeasonLeader.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new FranchiseSeasonLeaderStat.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new FranchiseSeasonLogo.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new FranchiseSeasonMetric.EntityConfiguration());

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

            modelBuilder.ApplyConfiguration(new CompetitionPlay.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new CompetitionPlayExternalId.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new PlayTypeCategory.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new PowerIndex.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new PredictionMetric.EntityConfiguration());

            modelBuilder.ApplyConfiguration(new SeasonFuture.EntityConfiguration());
            modelBuilder.ApplyConfiguration(new SeasonFutureExternalId.EntityConfiguration());
        }
    }
}
