using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using Xunit;
using Xunit.Abstractions;

namespace SportsData.Producer.Tests.Unit.Application.Competitions
{
    public class CompetitionMetricServiceTests : ProducerTestBase<CompetitionMetricsService>
    {
        private readonly ITestOutputHelper _output;

        public CompetitionMetricServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Diagnostic Tests

        [Fact]
        public async Task Diagnostic_VerifyJsonLoadsCorrectly()
        {
            // Arrange - Load JSON and convert to DTOs
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlays.json");
            var playDtos = json.FromJson<List<EspnEventCompetitionPlayDto>>();

            // Assert
            playDtos.Should().NotBeNull();
            playDtos.Should().NotBeEmpty();
            
            _output.WriteLine($"Total plays loaded: {playDtos!.Count}");
            
            // Check for scrimmage plays
            var scrimmagePlays = playDtos.Where(p => 
                p.Start?.Down >= 1 && 
                p.Start?.Down <= 4 &&
                p.Type?.Id != null).ToList();
            
            _output.WriteLine($"Scrimmage plays (with down 1-4): {scrimmagePlays.Count}");
            
            // Check for plays with team assignments
            var playsWithTeam = playDtos.Where(p => p.Start?.Team?.Ref != null).ToList();
            _output.WriteLine($"Plays with Start.Team: {playsWithTeam.Count}");
            
            // Check distribution by team
            var team99Plays = playDtos.Where(p => p.Start?.Team?.Ref?.ToString().Contains("/teams/99") == true).ToList();
            var team30Plays = playDtos.Where(p => p.Start?.Team?.Ref?.ToString().Contains("/teams/30") == true).ToList();
            
            _output.WriteLine($"Team 99 (USC) plays: {team99Plays.Count}");
            _output.WriteLine($"Team 30 (LSU) plays: {team30Plays.Count}");
            
            // Check red zone plays
            var rzPlays = playDtos.Where(p => 
                p.Start?.YardsToEndzone <= 20 && 
                p.Start?.YardsToEndzone > 0 &&
                p.Start?.Down >= 1).ToList();
            
            _output.WriteLine($"Red zone plays (yardsToEndzone <= 20): {rzPlays.Count}");
            
            // Check scoring plays
            var scoringPlays = playDtos.Where(p => p.ScoringPlay).ToList();
            _output.WriteLine($"Scoring plays: {scoringPlays.Count}");
            
            scrimmagePlays.Should().HaveCountGreaterThan(50);
            playsWithTeam.Should().HaveCountGreaterThan(100);
        }

        [Fact]
        public async Task Diagnostic_VerifyPlaysConvertToEntitiesCorrectly()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            var (_, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(competitionId);

            // Assert - Check plays were created
            var plays = await FootballDataContext.CompetitionPlays
                .Where(p => p.CompetitionId == competitionId)
                .ToListAsync();

            _output.WriteLine($"Total plays in database: {plays.Count}");
            
            // Check plays with franchise season IDs
            var playsWithFranchise = plays.Where(p => p.StartFranchiseSeasonId.HasValue).ToList();
            _output.WriteLine($"Plays with StartFranchiseSeasonId: {playsWithFranchise.Count}");
            
            var homePlays = plays.Where(p => p.StartFranchiseSeasonId == homeTeamId).ToList();
            var awayPlays = plays.Where(p => p.StartFranchiseSeasonId == awayTeamId).ToList();
            
            _output.WriteLine($"Home team (USC) plays: {homePlays.Count}");
            _output.WriteLine($"Away team (LSU) plays: {awayPlays.Count}");
            
            // Check play types
            var rushPlays = plays.Where(p => p.Type == PlayType.Rush).ToList();
            var passPlays = plays.Where(p => p.Type == PlayType.PassReception).ToList();
            var rushTdPlays = plays.Where(p => p.Type == PlayType.RushingTouchdown).ToList();
            var passTdPlays = plays.Where(p => p.Type == PlayType.PassingTouchdown).ToList();
            
            _output.WriteLine($"Rush plays: {rushPlays.Count}");
            _output.WriteLine($"Pass plays: {passPlays.Count}");
            _output.WriteLine($"Rushing TDs: {rushTdPlays.Count}");
            _output.WriteLine($"Passing TDs: {passTdPlays.Count}");
            
            // Check red zone plays
            var rzPlays = plays.Where(p => 
                p.StartYardsToEndzone.HasValue && 
                p.StartYardsToEndzone.Value <= 20 &&
                p.StartYardsToEndzone.Value > 0 &&
                p.StartDown >= 1).ToList();
            
            _output.WriteLine($"Red zone plays in DB: {rzPlays.Count}");
            
            plays.Should().HaveCountGreaterThan(100);
            playsWithFranchise.Should().HaveCountGreaterThan(50);
        }

        #endregion

        #region CalculateCompetitionMetrics Tests

        [Fact]
        public async Task CalculateCompetitionMetrics_WhenCompetitionNotFound_LogsErrorAndReturns()
        {
            // Arrange
            var nonExistentCompetitionId = Guid.NewGuid();
            var sut = Mocker.CreateInstance<CompetitionMetricsService>();

            // Act
            await sut.CalculateCompetitionMetrics(nonExistentCompetitionId);

            // Assert
            var metrics = await FootballDataContext.CompetitionMetrics.ToListAsync();
            metrics.Should().BeEmpty();
        }

        [Fact]
        public async Task CalculateCompetitionMetrics_WithRealGameData_CreatesMetricsForBothTeams()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            var (competition, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(competitionId);
            var sut = Mocker.CreateInstance<CompetitionMetricsService>();

            // Act
            await sut.CalculateCompetitionMetrics(competitionId);

            // Assert
            var metrics = await FootballDataContext.CompetitionMetrics.ToListAsync();
            metrics.Should().HaveCount(2);
            metrics.Should().Contain(m => m.FranchiseSeasonId == homeTeamId);
            metrics.Should().Contain(m => m.FranchiseSeasonId == awayTeamId);
        }

        #endregion

        #region YPP (Yards Per Play) Tests

        [Fact]
        public async Task CalculateYpp_WithRealGameData_CalculatesCorrectAverageForBothTeams()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            var (_, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(competitionId);
            var sut = Mocker.CreateInstance<CompetitionMetricsService>();

            // Act
            await sut.CalculateCompetitionMetrics(competitionId);

            // Assert
            var homeMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == homeTeamId);
            var awayMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == awayTeamId);
            
            _output.WriteLine($"Home YPP: {homeMetric.Ypp}");
            _output.WriteLine($"Away YPP: {awayMetric.Ypp}");
            
            // With real game data, both teams should have positive YPP
            homeMetric.Ypp.Should().BeGreaterThan(0);
            awayMetric.Ypp.Should().BeGreaterThan(0);
            
            // YPP typically ranges from 4-7 yards per play in college football
            homeMetric.Ypp.Should().BeInRange(3m, 10m);
            awayMetric.Ypp.Should().BeInRange(3m, 10m);
        }

        #endregion

        #region Success Rate Tests

        [Fact]
        public async Task CalculateSuccessRate_WithRealGameData_CalculatesRealisticRates()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            var (_, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(competitionId);
            var sut = Mocker.CreateInstance<CompetitionMetricsService>();

            // Act
            await sut.CalculateCompetitionMetrics(competitionId);

            // Assert
            var homeMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == homeTeamId);
            var awayMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == awayTeamId);
            
            _output.WriteLine($"Home Success Rate: {homeMetric.SuccessRate}");
            _output.WriteLine($"Away Success Rate: {awayMetric.SuccessRate}");
            
            // Success rate should be between 0 and 1
            homeMetric.SuccessRate.Should().BeInRange(0m, 1m);
            awayMetric.SuccessRate.Should().BeInRange(0m, 1m);
            
            // In a real game, success rate typically ranges from 0.30 to 0.55
            homeMetric.SuccessRate.Should().BeGreaterThan(0.2m);
            awayMetric.SuccessRate.Should().BeGreaterThan(0.2m);
        }

        #endregion

        #region Explosive Rate Tests

        [Fact]
        public async Task CalculateExplosiveRate_WithRealGameData_CalculatesRealisticRates()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            var (_, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(competitionId);
            var sut = Mocker.CreateInstance<CompetitionMetricsService>();

            // Act
            await sut.CalculateCompetitionMetrics(competitionId);

            // Assert
            var homeMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == homeTeamId);
            var awayMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == awayTeamId);
            
            _output.WriteLine($"Home Explosive Rate: {homeMetric.ExplosiveRate}");
            _output.WriteLine($"Away Explosive Rate: {awayMetric.ExplosiveRate}");
            
            // Explosive rate should be between 0 and 1
            homeMetric.ExplosiveRate.Should().BeInRange(0m, 1m);
            awayMetric.ExplosiveRate.Should().BeInRange(0m, 1m);
            
            // In a real game, explosive rate (plays >= 20 yards) is typically 0.05 to 0.15
            homeMetric.ExplosiveRate.Should().BeLessThan(0.3m);
            awayMetric.ExplosiveRate.Should().BeLessThan(0.3m);
        }

        #endregion

        #region Third/Fourth Conversion Rate Tests

        [Fact]
        public async Task CalculateThirdFourthConversionRate_WithRealGameData_CalculatesRealisticRates()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            var (_, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(competitionId);
            var sut = Mocker.CreateInstance<CompetitionMetricsService>();

            // Act
            await sut.CalculateCompetitionMetrics(competitionId);

            // Assert
            var homeMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == homeTeamId);
            var awayMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == awayTeamId);
            
            _output.WriteLine($"Home 3rd/4th Conversion Rate: {homeMetric.ThirdFourthRate}");
            _output.WriteLine($"Away 3rd/4th Conversion Rate: {awayMetric.ThirdFourthRate}");
            
            // Conversion rate should be between 0 and 1
            homeMetric.ThirdFourthRate.Should().BeInRange(0m, 1m);
            awayMetric.ThirdFourthRate.Should().BeInRange(0m, 1m);
        }

        #endregion

        #region Red Zone TD Rate Tests

        [Fact]
        public async Task CalculateRedZoneTdRate_WithRealGameData_HandlesRedZoneTrips()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            var (_, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(competitionId);
            var sut = Mocker.CreateInstance<CompetitionMetricsService>();

            // Act
            await sut.CalculateCompetitionMetrics(competitionId);

            // Assert
            var homeMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == homeTeamId);
            var awayMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == awayTeamId);
            
            _output.WriteLine($"Home RZ TD Rate: {homeMetric.RzTdRate?.ToString() ?? "null"}");
            _output.WriteLine($"Away RZ TD Rate: {awayMetric.RzTdRate?.ToString() ?? "null"}");
            
            // If there are red zone trips, the rate should be between 0 and 1
            if (homeMetric.RzTdRate.HasValue)
            {
                homeMetric.RzTdRate.Value.Should().BeInRange(0m, 1m);
            }
            
            if (awayMetric.RzTdRate.HasValue)
            {
                awayMetric.RzTdRate.Value.Should().BeInRange(0m, 1m);
            }
        }

        #endregion

        #region Red Zone Scoring Rate Tests

        [Fact]
        public async Task CalculateRedZoneScoringRate_WithRealGameData_HandlesRedZoneTrips()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            var (_, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(competitionId);
            var sut = Mocker.CreateInstance<CompetitionMetricsService>();

            // Act
            await sut.CalculateCompetitionMetrics(competitionId);

            // Assert
            var homeMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == homeTeamId);
            var awayMetric = await FootballDataContext.CompetitionMetrics
                .FirstAsync(m => m.FranchiseSeasonId == awayTeamId);
            
            _output.WriteLine($"Home RZ Scoring Rate: {homeMetric.RzScoreRate?.ToString() ?? "null"}");
            _output.WriteLine($"Away RZ Scoring Rate: {awayMetric.RzScoreRate?.ToString() ?? "null"}");
            
            // If there are red zone trips, the scoring rate should be between 0 and 1
            if (homeMetric.RzScoreRate.HasValue)
            {
                homeMetric.RzScoreRate.Value.Should().BeInRange(0m, 1m);
                // Scoring rate should always be >= TD rate (since scoring includes FGs)
                if (homeMetric.RzTdRate.HasValue)
                {
                    homeMetric.RzScoreRate.Value.Should().BeGreaterThanOrEqualTo(homeMetric.RzTdRate.Value);
                }
            }
            
            if (awayMetric.RzScoreRate.HasValue)
            {
                awayMetric.RzScoreRate.Value.Should().BeInRange(0m, 1m);
                if (awayMetric.RzTdRate.HasValue)
                {
                    awayMetric.RzScoreRate.Value.Should().BeGreaterThanOrEqualTo(awayMetric.RzTdRate.Value);
                }
            }
        }

        #endregion

        #region Comprehensive Test with Real Data

        [Fact]
        public async Task CalculateCompetitionMetrics_WithRealGameData_ProducesComprehensiveMetrics()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            var (_, homeTeamId, awayTeamId) = await SeedCompetitionWithRealGameDataAsync(competitionId);
            var sut = Mocker.CreateInstance<CompetitionMetricsService>();

            // Act
            await sut.CalculateCompetitionMetrics(competitionId);

            // Assert
            var metrics = await FootballDataContext.CompetitionMetrics.ToListAsync();
            metrics.Should().HaveCount(2);
            
            foreach (var metric in metrics)
            {
                var teamName = metric.FranchiseSeasonId == homeTeamId ? "Home (USC)" : "Away (LSU)";
                _output.WriteLine($"\n{teamName} Metrics:");
                _output.WriteLine($"  YPP: {metric.Ypp}");
                _output.WriteLine($"  Success Rate: {metric.SuccessRate}");
                _output.WriteLine($"  Explosive Rate: {metric.ExplosiveRate}");
                _output.WriteLine($"  3rd/4th Rate: {metric.ThirdFourthRate}");
                _output.WriteLine($"  RZ TD Rate: {metric.RzTdRate?.ToString() ?? "null"}");
                _output.WriteLine($"  RZ Score Rate: {metric.RzScoreRate?.ToString() ?? "null"}");
                
                // All base metrics should be calculated
                metric.Ypp.Should().BeGreaterThanOrEqualTo(0);
                metric.SuccessRate.Should().BeInRange(0m, 1m);
                metric.ExplosiveRate.Should().BeInRange(0m, 1m);
                metric.ThirdFourthRate.Should().BeInRange(0m, 1m);
                
                // Red zone metrics may be null if no trips occurred
                if (metric.RzTdRate.HasValue)
                {
                    metric.RzTdRate.Value.Should().BeInRange(0m, 1m);
                }
                
                if (metric.RzScoreRate.HasValue)
                {
                    metric.RzScoreRate.Value.Should().BeInRange(0m, 1m);
                }
            }
        }

        [Fact]
        public async Task CalculateCompetitionMetrics_WithRealGameData_PlaysAreOrderedCorrectly()
        {
            // Arrange
            var competitionId = Guid.NewGuid();
            await SeedCompetitionWithRealGameDataAsync(competitionId);

            // Act - Get plays ordered by sequence number
            var plays = await FootballDataContext.CompetitionPlays
                .Where(p => p.CompetitionId == competitionId)
                .OrderBy(p => p.SequenceNumber)
                .ToListAsync();

            // Assert
            plays.Should().NotBeEmpty();
            
            _output.WriteLine($"Total plays: {plays.Count}");
            _output.WriteLine($"First play sequence: {plays.First().SequenceNumber}");
            _output.WriteLine($"Last play sequence: {plays.Last().SequenceNumber}");
            
            // Track score progressions (note: ESPN data occasionally has score corrections/anomalies)
            int previousAwayScore = 0;
            int previousHomeScore = 0;
            int scoreDecreaseCount = 0;
            
            for (int i = 0; i < plays.Count; i++)
            {
                var play = plays[i];
                
                // Log any score decrease for debugging (ESPN data can have score corrections)
                if (play.AwayScore < previousAwayScore || play.HomeScore < previousHomeScore)
                {
                    scoreDecreaseCount++;
                    _output.WriteLine($"Score anomaly at play {i} (seq: {play.SequenceNumber}):");
                    _output.WriteLine($"  Previous: Away {previousAwayScore}, Home {previousHomeScore}");
                    _output.WriteLine($"  Current: Away {play.AwayScore}, Home {play.HomeScore}");
                    _output.WriteLine($"  Play: {play.Text}");
                }
                
                previousAwayScore = play.AwayScore;
                previousHomeScore = play.HomeScore;
            }
            
            _output.WriteLine($"Final score: Away {previousAwayScore}, Home {previousHomeScore}");
            _output.WriteLine($"Score anomalies found: {scoreDecreaseCount}");
            
            // ESPN data has been observed to have occasional score corrections/anomalies
            // We expect most plays to maintain score progression, but allow for a few anomalies
            scoreDecreaseCount.Should().BeLessThan(5, 
                "ESPN data should not have more than a few score anomalies");
            
            // Verify final scores are reasonable (both teams scored)
            previousAwayScore.Should().BeGreaterThan(0);
            previousHomeScore.Should().BeGreaterThan(0);
        }

        #endregion

        #region Helper Methods

        private async Task<(Competition competition, Guid homeTeamId, Guid awayTeamId)> SeedCompetitionWithRealGameDataAsync(Guid competitionId)
        {
            // Load real game data from JSON
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlays.json");
            var playDtos = json.FromJson<List<EspnEventCompetitionPlayDto>>();

            if (playDtos == null || !playDtos.Any())
            {
                throw new InvalidOperationException("Failed to load play data from JSON");
            }

            // Identify the two teams from the play data
            // Team IDs from the JSON are: 99 (USC) and 30 (LSU)
            var homeTeamId = Guid.NewGuid(); // USC (team 99 in JSON)
            var awayTeamId = Guid.NewGuid(); // LSU (team 30 in JSON)

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            // Create franchise seasons for both teams
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

            // Create external IDs for franchise seasons so plays can resolve team IDs
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

            // Convert DTOs to CompetitionPlay entities
            var plays = new List<CompetitionPlay>();
            
            foreach (var dto in playDtos)
            {
                // Determine which team is on offense for this play
                Guid? startFranchiseSeasonId = null;
                Guid? endFranchiseSeasonId = null;

                // The Start.Team.Ref tells us which team is on offense
                if (dto.Start?.Team?.Ref != null)
                {
                    var teamIdStr = dto.Start.Team.Ref.ToString();
                    if (teamIdStr.Contains("/teams/99"))
                    {
                        startFranchiseSeasonId = homeTeamId;
                    }
                    else if (teamIdStr.Contains("/teams/30"))
                    {
                        startFranchiseSeasonId = awayTeamId;
                    }
                }

                // The End.Team.Ref tells us which team has possession after the play
                if (dto.End?.Team?.Ref != null)
                {
                    var teamIdStr = dto.End.Team.Ref.ToString();
                    if (teamIdStr.Contains("/teams/99"))
                    {
                        endFranchiseSeasonId = homeTeamId;
                    }
                    else if (teamIdStr.Contains("/teams/30"))
                    {
                        endFranchiseSeasonId = awayTeamId;
                    }
                }

                var play = dto.AsEntity(
                    generator,
                    Guid.NewGuid(), // correlationId
                    competitionId,
                    null, // driveId
                    startFranchiseSeasonId,
                    endFranchiseSeasonId);

                plays.Add(play);
            }

            await FootballDataContext.CompetitionPlays.AddRangeAsync(plays);
            await FootballDataContext.SaveChangesAsync();

            return (competition, homeTeamId, awayTeamId);
        }

        #endregion
    }
}
