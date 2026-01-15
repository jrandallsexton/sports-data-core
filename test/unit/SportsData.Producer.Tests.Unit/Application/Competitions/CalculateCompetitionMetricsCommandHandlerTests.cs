#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
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
        private Competition? _competition;

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

            _output.WriteLine($"Test data initialized: Competition {_competitionId}");
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

        private async Task<(Competition competition, Guid homeTeamId, Guid awayTeamId)> SeedCompetitionWithRealGameDataAsync(Guid competitionId)
        {
            // Load JSON once and cache it
            if (_cachedJson == null)
            {
                _cachedJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlays.json");
                _output.WriteLine("JSON loaded and cached");
            }

            var playDtos = _cachedJson.FromJson<List<EspnEventCompetitionPlayDto>>();

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
            var contest = Fixture.Build<Contest>()
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
            var competition = Fixture.Build<Competition>()
                .With(x => x.Id, competitionId)
                .With(x => x.ContestId, contest.Id)
                .With(x => x.Contest, contest)
                .Without(x => x.Plays)
                .Without(x => x.Drives)
                .Without(x => x.ExternalIds)
                .Create();

            await FootballDataContext.Competitions.AddAsync(competition);

            // Convert DTOs to entities (batch process for performance)
            var plays = new List<CompetitionPlay>(playDtos.Count);
            
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

                var play = dto.AsEntity(
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

