using SportsData.Producer.Infrastructure.Data.Football.Entities;
#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

using Xunit;
using Xunit.Abstractions;

namespace SportsData.Producer.Tests.Unit.Application.Competitions
{
    /// <summary>
    /// Tests for CalculateCompetitionMetricsCommandHandler using real game data.
    /// Optimized to reduce test setup overhead by consolidating related tests.
    /// </summary>
    [Collection("Sequential")] // Force sequential to avoid DB contention
    public class CalculateCompetitionMetricsCommandHandlerTests : ProducerTestBase<CalculateCompetitionMetricsCommandHandler>, IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        
        // Shared test data - populated once in InitializeAsync per test class instance
        private Guid _competitionId;
        private Guid _homeTeamId;
        private Guid _awayTeamId;
        private FootballCompetition? _competition;

        public CalculateCompetitionMetricsCommandHandlerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Initialize expensive test data once before tests run.
        /// Note: xUnit creates a new instance per test, so this runs multiple times.
        /// The key optimization is reducing the number of tests from 12 to 4.
        /// </summary>
        public async Task InitializeAsync()
        {
            _competitionId = Guid.NewGuid();
            var (competition, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(_competitionId);
            
            _competition = competition;
            _homeTeamId = homeTeamId;
            _awayTeamId = awayTeamId;

            _output.WriteLine($"Test data initialized: CompetitionBase {_competitionId}");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        #region CalculateCompetitionMetrics Tests

        [Fact]
        public async Task ExecuteAsync_WhenCompetitionNotFound_ReturnsFailure()
        {
            // Arrange
            var nonExistentCompetitionId = Guid.NewGuid();
            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            var command = new CalculateCompetitionMetricsCommand(nonExistentCompetitionId);

            // Act
            var result = await sut.ExecuteAsync(command, CancellationToken.None);

            // Assert
            result.Should().BeOfType<Failure<Guid>>();
            result.Status.Should().Be(ResultStatus.NotFound);

            var metrics = await FootballDataContext.CompetitionMetrics
                .Where(m => m.CompetitionId == nonExistentCompetitionId)
                .ToListAsync();
            metrics.Should().BeEmpty();
        }

        [Fact]
        public async Task ExecuteAsync_WithRealGameData_CreatesMetricsForBothTeams()
        {
            // Arrange
            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            var command = new CalculateCompetitionMetricsCommand(_competitionId);

            // Act
            var result = await sut.ExecuteAsync(command, CancellationToken.None);

            // Assert
            result.Should().BeOfType<Success<Guid>>();
            result.Value.Should().Be(_competitionId);

            var metrics = await FootballDataContext.CompetitionMetrics
                .Where(m => m.CompetitionId == _competitionId)
                .ToListAsync();

            metrics.Should().HaveCount(2);
            metrics.Should().Contain(m => m.FranchiseSeasonId == _homeTeamId);
            metrics.Should().Contain(m => m.FranchiseSeasonId == _awayTeamId);
        }

        #endregion

        #region Comprehensive Metrics Validation

        /// <summary>
        /// Comprehensive test that validates all calculated metrics in one pass.
        /// This replaces multiple individual metric tests to improve performance.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_WithRealGameData_ProducesValidMetricsForAllCategories()
        {
            // Arrange
            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            var command = new CalculateCompetitionMetricsCommand(_competitionId);

            // Act
            var result = await sut.ExecuteAsync(command, CancellationToken.None);

            // Assert
            result.Should().BeOfType<Success<Guid>>();

            var homeMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == _homeTeamId);
            var awayMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == _awayTeamId);

            // Validate all metrics for both teams
            ValidateAllMetrics(homeMetric, "Home (USC)");
            ValidateAllMetrics(awayMetric, "Away (LSU)");
        }

        private void ValidateAllMetrics(CompetitionMetric metric, string teamName)
        {
            _output.WriteLine($"\n{teamName} Metrics:");
            
            // YPP (Yards Per Play)
            _output.WriteLine($"  YPP: {metric.Ypp}");
            metric.Ypp.Should().BeGreaterThan(0, "should have positive YPP");
            metric.Ypp.Should().BeInRange(3m, 10m, "YPP typically ranges 3-10 in college football");
            
            // Success Rate
            _output.WriteLine($"  Success Rate: {metric.SuccessRate}");
            metric.SuccessRate.Should().BeInRange(0m, 1m, "success rate is a percentage");
            metric.SuccessRate.Should().BeGreaterThan(0.2m, "real games typically have >20% success rate");
            
            // Explosive Rate
            _output.WriteLine($"  Explosive Rate: {metric.ExplosiveRate}");
            metric.ExplosiveRate.Should().BeInRange(0m, 1m, "explosive rate is a percentage");
            metric.ExplosiveRate.Should().BeLessThan(0.3m, "explosive plays (20+ yds) are rare");
            
            // Third/Fourth Down Conversion Rate
            _output.WriteLine($"  3rd/4th Conversion Rate: {metric.ThirdFourthRate}");
            metric.ThirdFourthRate.Should().BeInRange(0m, 1m, "conversion rate is a percentage");
            
            // Red Zone TD Rate
            _output.WriteLine($"  RZ TD Rate: {metric.RzTdRate?.ToString() ?? "null"}");
            if (metric.RzTdRate.HasValue)
            {
                metric.RzTdRate.Value.Should().BeInRange(0m, 1m, "RZ TD rate is a percentage");
            }
            
            // Red Zone Scoring Rate
            _output.WriteLine($"  RZ Score Rate: {metric.RzScoreRate?.ToString() ?? "null"}");
            if (metric.RzScoreRate.HasValue)
            {
                metric.RzScoreRate.Value.Should().BeInRange(0m, 1m, "RZ score rate is a percentage");
                
                // Scoring rate should be >= TD rate (includes FGs)
                if (metric.RzTdRate.HasValue)
                {
                    metric.RzScoreRate.Value.Should().BeGreaterThanOrEqualTo(metric.RzTdRate.Value,
                        "scoring rate includes both TDs and FGs");
                }
            }
            
            // All base metrics should have valid values
            metric.Id.Should().NotBeEmpty();
            metric.CompetitionId.Should().NotBeEmpty();
            metric.FranchiseSeasonId.Should().NotBeEmpty();
        }

        #endregion

        #region Data Integrity Tests

        [Fact]
        public async Task CalculateCompetitionMetrics_WithRealGameData_PlaysAreOrderedCorrectly()
        {
            // Arrange & Act - Using pre-seeded data
            var plays = await FootballDataContext.CompetitionPlays
                .Where(p => p.CompetitionId == _competitionId)
                .OrderBy(p => p.SequenceNumber)
                .ToListAsync();

            // Assert
            plays.Should().NotBeEmpty();
            
            _output.WriteLine($"Total plays: {plays.Count}");
            _output.WriteLine($"First play sequence: {plays.First().SequenceNumber}");
            _output.WriteLine($"Last play sequence: {plays.Last().SequenceNumber}");
            
            // Track score progressions (note: ESPN data occasionally has score corrections)
            int previousAwayScore = 0;
            int previousHomeScore = 0;
            int scoreAnomalyCount = 0;
            
            for (int i = 0; i < plays.Count; i++)
            {
                var play = plays[i];
                
                // Log score decreases (ESPN data can have corrections)
                if (play.AwayScore < previousAwayScore || play.HomeScore < previousHomeScore)
                {
                    scoreAnomalyCount++;
                }
                
                previousAwayScore = play.AwayScore;
                previousHomeScore = play.HomeScore;
            }
            
            _output.WriteLine($"Final score: Away {previousAwayScore}, Home {previousHomeScore}");
            _output.WriteLine($"Score anomalies found: {scoreAnomalyCount}");
            
            // ESPN data should not have many anomalies
            scoreAnomalyCount.Should().BeLessThan(5, "ESPN data should have minimal score anomalies");
            
            // Verify both teams scored
            previousAwayScore.Should().BeGreaterThan(0, "away team should have scored");
            previousHomeScore.Should().BeGreaterThan(0, "home team should have scored");
        }

        #endregion

        #region Helper Methods

        private static string? _cachedJson; // Cache JSON across test instances

        #region Audit regression fixtures (C1 / C2)

        /// <summary>
        /// AUDIT C1: a one-play drive must contribute the POINTS SCORED ON
        /// THAT DRIVE, not the team's cumulative score. Fixture: home
        /// scores a 2-play TD drive (0 -> 7), later takes a 1-play kneel
        /// drive still leading 7-0. Correct PPD = (7 + 0) / 2 = 3.5; the
        /// pre-fix code booked (7 + 7) / 2 = 7 (kneel credited with the
        /// scoreboard).
        /// </summary>
        [Fact]
        public async Task PointsPerDrive_OnePlayDrive_DoesNotInheritCumulativeScore()
        {
            var (competitionId, homeId, _) = await SeedSyntheticAsync(
                (homeOffense: true,  driveKey: 1, seq: "01", homeScore: 0, awayScore: 0, yte: (int?)null),
                (homeOffense: true,  driveKey: 1, seq: "02", homeScore: 7, awayScore: 0, yte: null),
                (homeOffense: false, driveKey: 2, seq: "03", homeScore: 7, awayScore: 0, yte: null),
                (homeOffense: true,  driveKey: 3, seq: "04", homeScore: 7, awayScore: 0, yte: null));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var home = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => new { m.PointsPerDrive })
                .SingleAsync();

            home.PointsPerDrive.Should().Be(3.5m,
                "TD drive (7) + kneel drive (0) over 2 drives — not the kneel inheriting the scoreboard");
        }

        /// <summary>
        /// AUDIT C2: FieldPosDiff must be computed from orientation-free
        /// yards-to-endzone, not raw stadium-oriented yard lines. Fixture:
        /// home drives start at own 30 (YTE 70), away drives at own 20
        /// (YTE 80) — home diff = +10, away diff = -10, symmetric.
        /// </summary>
        [Fact]
        public async Task FieldPosDiff_UsesOrientationFreeYardsToEndzone()
        {
            var (competitionId, homeId, awayId) = await SeedSyntheticAsync(
                (homeOffense: true,  driveKey: 1, seq: "01", homeScore: 0, awayScore: 0, yte: (int?)70),
                (homeOffense: false, driveKey: 2, seq: "02", homeScore: 0, awayScore: 0, yte: 80),
                (homeOffense: true,  driveKey: 3, seq: "03", homeScore: 0, awayScore: 0, yte: 70),
                (homeOffense: false, driveKey: 4, seq: "04", homeScore: 0, awayScore: 0, yte: 80));

            // An unknown-offense drive (data gap) must be excluded from
            // BOTH sides' averages, not silently counted as "opponent".
            await FootballDataContext.Drives.AddAsync(new CompetitionDrive
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                StartFranchiseSeasonId = null,
                StartYardsToEndzone = 1,
                Description = "unknown offense",
                SequenceNumber = "99",
                Ordinal = 99
            });
            await FootballDataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var home = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => new { m.FieldPosDiff })
                .SingleAsync();
            var away = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == awayId)
                .Select(m => new { m.FieldPosDiff })
                .SingleAsync();

            home.FieldPosDiff.Should().Be(10m,
                "home starts at own 30 vs away's own 20 — and the unknown-offense drive is excluded from both sides");
            away.FieldPosDiff.Should().Be(-10m, "symmetric opposite of home");
        }

        /// <summary>
        /// Minimal synthetic competition: one play per tuple, each play in
        /// its own (or shared) drive. yte, when set, becomes the DRIVE's
        /// StartYardsToEndzone (C2 input); scores are the cumulative
        /// scoreboard (C1 input).
        /// </summary>
        private async Task<(Guid competitionId, Guid homeId, Guid awayId)> SeedSyntheticAsync(
            params (bool homeOffense, int driveKey, string seq, int homeScore, int awayScore, int? yte)[] playSpecs)
        {
            var competitionId = Guid.NewGuid();
            var homeId = Guid.NewGuid();
            var awayId = Guid.NewGuid();

            var homeFs = Fixture.Build<FranchiseSeason>()
                .With(x => x.Id, homeId).With(x => x.FranchiseId, Guid.NewGuid())
                .With(x => x.SeasonYear, 2025).Without(x => x.ExternalIds).Create();
            var awayFs = Fixture.Build<FranchiseSeason>()
                .With(x => x.Id, awayId).With(x => x.FranchiseId, Guid.NewGuid())
                .With(x => x.SeasonYear, 2025).Without(x => x.ExternalIds).Create();
            await FootballDataContext.FranchiseSeasons.AddRangeAsync(homeFs, awayFs);

            var contest = Fixture.Build<FootballContest>()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.HomeTeamFranchiseSeasonId, homeId)
                .With(x => x.AwayTeamFranchiseSeasonId, awayId)
                .With(x => x.HomeTeamFranchiseSeason, homeFs)
                .With(x => x.AwayTeamFranchiseSeason, awayFs)
                .Without(x => x.Links).Without(x => x.ExternalIds).Without(x => x.Competitions)
                .Create();
            await FootballDataContext.Contests.AddAsync(contest);

            var competition = Fixture.Build<FootballCompetition>()
                .With(x => x.Id, competitionId)
                .With(x => x.ContestId, contest.Id)
                .With(x => x.Contest, contest)
                .Without(x => x.Plays).Without(x => x.Drives)
                .Without(x => x.Links).Without(x => x.ExternalIds)
                .Create();
            await FootballDataContext.Competitions.AddAsync(competition);

            var driveIds = new Dictionary<int, Guid>();
            var ordinal = 0;
            foreach (var spec in playSpecs)
            {
                var offenseId = spec.homeOffense ? homeId : awayId;

                if (!driveIds.TryGetValue(spec.driveKey, out var driveId))
                {
                    driveId = Guid.NewGuid();
                    driveIds[spec.driveKey] = driveId;
                    await FootballDataContext.Drives.AddAsync(new CompetitionDrive
                    {
                        Id = driveId,
                        CompetitionId = competitionId,
                        StartFranchiseSeasonId = offenseId,
                        StartYardsToEndzone = spec.yte,
                        Description = $"drive {spec.driveKey}",
                        SequenceNumber = spec.driveKey.ToString("00"),
                        Ordinal = ++ordinal
                    });
                }

                await FootballDataContext.CompetitionPlays.AddAsync(new FootballCompetitionPlay
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    DriveId = driveId,
                    EspnId = spec.seq,
                    SequenceNumber = spec.seq,
                    TypeId = "5",
                    Type = PlayType.Rush,
                    Text = "synthetic",
                    StartFranchiseSeasonId = offenseId,
                    HomeScore = spec.homeScore,
                    AwayScore = spec.awayScore
                });
            }

            await FootballDataContext.SaveChangesAsync();
            return (competitionId, homeId, awayId);
        }

        #endregion

        private async Task<(FootballCompetition competition, Guid homeTeamId, Guid awayTeamId)> SeedCompetitionWithRealGameDataAsync(Guid competitionId)
        {
            // Load JSON once and cache it
            if (_cachedJson == null)
            {
                _cachedJson = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlays.json");
                _output.WriteLine("JSON loaded and cached");
            }

            var playDtos = _cachedJson.FromJson<List<EspnFootballEventCompetitionPlayDto>>();

            if (playDtos == null || !playDtos.Any())
            {
                throw new InvalidOperationException("Failed to load play data from JSON");
            }

            // Team IDs: 99 (USC - home), 30 (LSU - away)
            var homeTeamId = Guid.NewGuid();
            var awayTeamId = Guid.NewGuid();

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            // Create franchise seasons
            var homeFranchiseSeason = Fixture.Build<FranchiseSeason>()
                .With(x => x.Id, homeTeamId)
                .With(x => x.FranchiseId, Guid.NewGuid())
                .With(x => x.SeasonYear, 2024)
                .Without(x => x.ExternalIds)
                .Create();

            var awayFranchiseSeason = Fixture.Build<FranchiseSeason>()
                .With(x => x.Id, awayTeamId)
                .With(x => x.FranchiseId, Guid.NewGuid())
                .With(x => x.SeasonYear, 2024)
                .Without(x => x.ExternalIds)
                .Create();

            await FootballDataContext.FranchiseSeasons.AddRangeAsync(homeFranchiseSeason, awayFranchiseSeason);

            // Create external IDs
            var homeExternalId = new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = homeTeamId,
                Provider = SourceDataProvider.Espn,
                SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99",
                SourceUrlHash = generator.Generate(new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99")).UrlHash,
                Value = generator.Generate(new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99")).UrlHash
            };

            var awayExternalId = new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = awayTeamId,
                Provider = SourceDataProvider.Espn,
                SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30",
                SourceUrlHash = generator.Generate(new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30")).UrlHash,
                Value = generator.Generate(new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30")).UrlHash
            };

            await FootballDataContext.FranchiseSeasonExternalIds.AddRangeAsync(homeExternalId, awayExternalId);

            // Create contest
            var contest = Fixture.Build<FootballContest>()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.HomeTeamFranchiseSeasonId, homeTeamId)
                .With(x => x.AwayTeamFranchiseSeasonId, awayTeamId)
                .With(x => x.HomeTeamFranchiseSeason, homeFranchiseSeason)
                .With(x => x.AwayTeamFranchiseSeason, awayFranchiseSeason)
                .Without(x => x.Links)
                .Without(x => x.ExternalIds)
                .Without(x => x.Competitions)
                .Create();

            await FootballDataContext.Contests.AddAsync(contest);

            // Create competition
            var competition = Fixture.Build<FootballCompetition>()
                .With(x => x.Id, competitionId)
                .With(x => x.ContestId, contest.Id)
                .With(x => x.Contest, contest)
                .Without(x => x.Plays)
                .Without(x => x.Drives)
                .Without(x => x.ExternalIds)
                .Create();

            await FootballDataContext.Competitions.AddAsync(competition);

            // Convert DTOs to entities (batch process for performance)
            var plays = new List<FootballCompetitionPlay>(playDtos.Count);
            
            foreach (var dto in playDtos)
            {
                Guid? startFranchiseSeasonId = null;
                Guid? endFranchiseSeasonId = null;

                if (dto.Start?.Team?.Ref != null)
                {
                    var teamIdStr = dto.Start.Team.Ref.ToString();
                    startFranchiseSeasonId = teamIdStr.Contains("/teams/99") ? homeTeamId :
                                            teamIdStr.Contains("/teams/30") ? awayTeamId : null;
                }

                if (dto.End?.Team?.Ref != null)
                {
                    var teamIdStr = dto.End.Team.Ref.ToString();
                    endFranchiseSeasonId = teamIdStr.Contains("/teams/99") ? homeTeamId :
                                          teamIdStr.Contains("/teams/30") ? awayTeamId : null;
                }

                var play = dto.AsFootballEntity(
                    generator,
                    Guid.NewGuid(),
                    competitionId,
                    null,
                    startFranchiseSeasonId,
                    endFranchiseSeasonId);

                plays.Add(play);
            }

            await FootballDataContext.CompetitionPlays.AddRangeAsync(plays);
            await FootballDataContext.SaveChangesAsync();

            _output.WriteLine($"Seeded {plays.Count} plays for competition {competitionId}");

            return (competition, homeTeamId, awayTeamId);
        }

        #endregion
    }
}

