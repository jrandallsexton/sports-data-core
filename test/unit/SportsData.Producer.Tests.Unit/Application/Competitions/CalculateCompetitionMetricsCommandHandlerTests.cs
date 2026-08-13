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

        #region Audit regression fixtures (H1 / H2)

        /// <summary>
        /// AUDIT H1: an interception is an offensive SNAP (denominator)
        /// whose StatYardage is the DEFENSIVE RETURN (never the
        /// numerator). Fixture: rush 8 (success), rush 2 (fail), INT with
        /// a 40-yard return -> 3 snaps, 10 yards, no explosive play.
        /// Pre-fix: 2 snaps, Ypp 5.0, and a 40-yard "gain".
        /// </summary>
        [Fact]
        public async Task H1_InterceptionJoinsDenominator_ReturnYardsNeverCount()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 8),
                Play(home: true, drive: 1, seq: "02", type: PlayType.Rush, down: 1, dist: 10, yds: 2),
                Play(home: true, drive: 1, seq: "03", type: PlayType.PassInterceptionReturn, down: 1, dist: 10, yds: 40));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var home = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => new { m.Ypp, m.SuccessRate, m.ExplosiveRate })
                .SingleAsync();

            home.Ypp.Should().BeApproximately(10m / 3m, 0.0001m, "3 snaps, 10 offensive yards — the return is not a gain");
            home.SuccessRate.Should().BeApproximately(1m / 3m, 0.0001m, "only the 8-yard rush succeeds; the INT is a failed snap");
            home.ExplosiveRate.Should().Be(0m, "a 40-yard RETURN is not a 40-yard offensive gain");
        }

        /// <summary>
        /// AUDIT H1: a third-down interception is a FAILED conversion
        /// attempt — pre-fix it vanished from the denominator entirely.
        /// </summary>
        [Fact]
        public async Task H1_ThirdDownInterception_IsAFailedAttempt()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 3, dist: 2, yds: 5),
                Play(home: true, drive: 1, seq: "02", type: PlayType.PassInterceptionReturn, down: 3, dist: 5, yds: 40));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var rate = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => m.ThirdFourthRate)
                .SingleAsync();

            rate.Should().Be(0.5m, "one conversion in TWO attempts — the 40-yard return does not convert 3rd-and-5");
        }

        /// <summary>
        /// AUDIT H2 (the stale-trip regression): a red-zone trip that ends
        /// without a score must NOT be credited when the same offense
        /// scores on a LATER drive. Pre-fix, the trip stayed open until an
        /// opponent snap, absorbing the next drive's touchdown.
        /// </summary>
        [Fact]
        public async Task H2_StaleTrip_NotCreditedAcrossOwnNewDrive()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 2, yte: 15),
                // new drive, outside the red zone, ends in a TD
                Play(home: true, drive: 2, seq: "02", type: PlayType.Rush, down: 1, dist: 10, yds: 5, yte: 60),
                Play(home: true, drive: 2, seq: "03", type: PlayType.PassingTouchdown, down: 1, dist: 10, yds: 55, yte: 55));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var rz = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => new { m.RzTdRate, m.RzScoreRate })
                .SingleAsync();

            rz.RzTdRate.Should().Be(0m, "the red-zone trip ended scoreless when its drive ended");
            rz.RzScoreRate.Should().Be(0m);
        }

        /// <summary>
        /// AUDIT H2: a trip legitimately spans Q1->Q2 (same drive) — the
        /// quarter boundary must NOT terminate it.
        /// </summary>
        [Fact]
        public async Task H2_TripSurvivesQuarterBoundary_WithinSameHalf()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 3, yte: 18, period: 1),
                Play(home: true, drive: 1, seq: "02", type: PlayType.RushingTouchdown, down: 2, dist: 7, yds: 15, yte: 15, period: 2));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var rate = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => m.RzTdRate)
                .SingleAsync();

            rate.Should().Be(1m, "the trip crossed Q1->Q2 inside one drive and scored");
        }

        /// <summary>
        /// AUDIT H2: a trip does NOT survive halftime — even when the data
        /// (glitch) keeps the same DriveId across the break, a Q3 score
        /// cannot credit a Q2 trip.
        /// </summary>
        [Fact]
        public async Task H2_TripClosesAtTheHalf_EvenWithSameDriveId()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 2, yte: 15, period: 2),
                Play(home: true, drive: 1, seq: "02", type: PlayType.RushingTouchdown, down: 1, dist: 10, yds: 12, yte: 12, period: 3));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var rate = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => m.RzTdRate)
                .SingleAsync();

            // the Q2 trip closed scoreless at the half; the Q3 TD play (yte
            // 12, scrimmage snap) STARTS a second trip which scores.
            rate.Should().Be(0.5m, "Q2 trip scoreless; Q3 trip (new, same drive id) scores");
        }

        /// <summary>
        /// AUDIT H2: the two rates share one machine but different scoring
        /// predicates — a made field goal is a scoring trip, not a TD trip.
        /// Duplicate play events (same sequence) leave every count
        /// unchanged. (The machine is idempotent under adjacent replays by
        /// construction — the explicit sequence guard encodes the contract;
        /// this fixture documents the invariant rather than a reachable
        /// failure mode.)
        /// </summary>
        [Fact]
        public async Task H2_FieldGoalScoresTheTrip_ForScoreRateOnly()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 2, yte: 18),
                // duplicate of the trip-opening red-zone snap
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 2, yte: 18),
                Play(home: true, drive: 1, seq: "02", type: PlayType.FieldGoalGood, down: 4, dist: 8, yds: 33, yte: 16),
                // duplicate of the scoring play
                Play(home: true, drive: 1, seq: "02", type: PlayType.FieldGoalGood, down: 4, dist: 8, yds: 33, yte: 16));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var rz = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => new { m.RzTdRate, m.RzScoreRate })
                .SingleAsync();

            rz.RzScoreRate.Should().Be(1m, "the field goal scores the trip");
            rz.RzTdRate.Should().Be(0m, "a field goal is not a touchdown trip");
        }

        /// <summary>
        /// AUDIT H2 rule 1 (pre-existing rule, regression guard): an
        /// opponent standing scrimmage snap closes the trip — a LATER score
        /// on the same DriveId must not credit the closed trip.
        /// </summary>
        [Fact]
        public async Task H2_TripClosesOnOpponentStandingSnap()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 2, yte: 15),
                // opponent takes a standing scrimmage snap -> home trip closes scoreless
                Play(home: false, drive: 2, seq: "02", type: PlayType.Rush, down: 1, dist: 10, yds: 4, yte: 70),
                // home scores later on the ORIGINAL DriveId (data glitch):
                // must not credit the closed trip; yte 40 opens no new trip
                Play(home: true, drive: 1, seq: "03", type: PlayType.PassingTouchdown, down: 1, dist: 10, yds: 40, yte: 40));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var rate = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => m.RzTdRate)
                .SingleAsync();

            rate.Should().Be(0m, "the trip closed scoreless when the opponent snapped");
        }

        /// <summary>
        /// AUDIT H2 rule 4: a trip still open when the input ends closes at
        /// EOF and counts — the rate is 0, not null.
        /// </summary>
        [Fact]
        public async Task H2_TripOpenAtEndOfInput_CountsScoreless()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 3, yte: 12));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var rz = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => new { m.RzTdRate, m.RzScoreRate })
                .SingleAsync();

            rz.RzTdRate.Should().Be(0m, "the EOF-closed trip counts as a scoreless trip");
            rz.RzScoreRate.Should().Be(0m);
        }

        /// <summary>
        /// AUDIT H1+H2 (defensive TD): a red-zone pick-six ends the trip
        /// scoreless via the ensuing new drive, contributes a SNAP at zero
        /// offensive yards, and its 95-yard RETURN is never explosive.
        /// </summary>
        [Fact]
        public async Task H1H2_RedZonePickSix_ScorelessTrip_ZeroYardSnap()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 2, yte: 15),
                Play(home: true, drive: 1, seq: "02", type: PlayType.InterceptionReturnTouchdown, down: 2, dist: 8, yds: 95, yte: 13),
                // home's next possession (outside the RZ) closes the stale trip
                Play(home: true, drive: 2, seq: "03", type: PlayType.Rush, down: 1, dist: 10, yds: 5, yte: 60));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var home = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => new { m.RzTdRate, m.Ypp, m.ExplosiveRate })
                .SingleAsync();

            home.RzTdRate.Should().Be(0m, "a pick-six is the DEFENSE's touchdown, never the trip's");
            home.Ypp.Should().BeApproximately(7m / 3m, 0.0001m, "3 snaps, 7 offensive yards — the 95-yard return contributes zero");
            home.ExplosiveRate.Should().Be(0m);
        }

        /// <summary>
        /// AUDIT H2 rule 3: every overtime period is its own possession
        /// bucket — a trip opened in OT1 must not absorb an OT2 score, even
        /// on the same DriveId.
        /// </summary>
        [Fact]
        public async Task H2_TripClosesBetweenOvertimePeriods()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 2, yte: 15, period: 5),
                Play(home: true, drive: 1, seq: "02", type: PlayType.RushingTouchdown, down: 1, dist: 10, yds: 12, yte: 12, period: 6));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var rate = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => m.RzTdRate)
                .SingleAsync();

            // the OT1 trip closed scoreless at the OT break; the OT2 TD play
            // (yte 12) starts a second trip which scores.
            rate.Should().Be(0.5m, "OT1 trip scoreless; OT2 trip (new, same drive id) scores");
        }

        /// <summary>
        /// AUDIT H1: a lost fumble returned for a defensive touchdown is an
        /// offensive snap at zero yards — the return yardage never counts.
        /// </summary>
        [Fact]
        public async Task H1_FumbleReturnTouchdown_ZeroYardSnap()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: true, drive: 1, seq: "01", type: PlayType.Rush, down: 1, dist: 10, yds: 8),
                Play(home: true, drive: 1, seq: "02", type: PlayType.FumbleReturnTouchdown, down: 2, dist: 2, yds: 60));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var home = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => new { m.Ypp, m.ExplosiveRate })
                .SingleAsync();

            home.Ypp.Should().Be(4m, "2 snaps, 8 offensive yards — the 60-yard return contributes zero");
            home.ExplosiveRate.Should().Be(0m);
        }

        /// <summary>
        /// AUDIT H4 (ordering): SequenceNumber is a STRING — lexicographic
        /// order puts "10" before "2" and "9". The trip machine must see
        /// temporal (numeric) order. Numerically: opponent snap ("2"), home
        /// opens a trip ("9"), same-drive TD ("10") scores it -> 1.0.
        /// Lexicographically the TD is processed FIRST, the opponent snap
        /// closes that trip, and "9" opens a second scoreless trip -> 0.5.
        /// </summary>
        [Fact]
        public async Task H4_UnpaddedSequenceNumbers_ProcessedInNumericOrder()
        {
            var (competitionId, homeId, _) = await SeedPlaysAsync(
                Play(home: false, drive: 2, seq: "2", type: PlayType.Rush, down: 1, dist: 10, yds: 3, yte: 75),
                Play(home: true, drive: 1, seq: "9", type: PlayType.Rush, down: 1, dist: 10, yds: 2, yte: 15),
                Play(home: true, drive: 1, seq: "10", type: PlayType.RushingTouchdown, down: 2, dist: 8, yds: 13, yte: 13));

            var sut = Mocker.CreateInstance<CalculateCompetitionMetricsCommandHandler>();
            await sut.ExecuteAsync(new CalculateCompetitionMetricsCommand(competitionId), CancellationToken.None);

            var rate = await FootballDataContext.CompetitionMetrics
                .AsNoTracking()
                .Where(m => m.CompetitionId == competitionId && m.FranchiseSeasonId == homeId)
                .Select(m => m.RzTdRate)
                .SingleAsync();

            rate.Should().Be(1m, "one trip, opened at seq 9 and scored at seq 10 — temporal order, not string order");
        }

        private sealed record PlaySpec(
            bool Home, int Drive, string Seq, PlayType Type,
            int? Down, int? Dist, int Yds, int? Yte, int Period);

        private static PlaySpec Play(
            bool home, int drive, string seq, PlayType type,
            int? down = null, int? dist = null, int yds = 0, int? yte = null, int period = 1)
            => new(home, drive, seq, type, down, dist, yds, yte, period);

        /// <summary>
        /// Play-level synthetic competition for formula fixtures: full
        /// control of type/down/distance/yardage/yards-to-endzone/period.
        /// No drive rows are needed by the metrics under test here.
        /// </summary>
        private async Task<(Guid competitionId, Guid homeId, Guid awayId)> SeedPlaysAsync(
            params PlaySpec[] specs)
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
            var i = 0;
            foreach (var spec in specs)
            {
                if (!driveIds.TryGetValue(spec.Drive, out var driveId))
                {
                    driveId = Guid.NewGuid();
                    driveIds[spec.Drive] = driveId;
                }

                await FootballDataContext.CompetitionPlays.AddAsync(new FootballCompetitionPlay
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    DriveId = driveId,
                    EspnId = $"p{++i}-{spec.Seq}",
                    SequenceNumber = spec.Seq,
                    TypeId = "0",
                    Type = spec.Type,
                    Text = "synthetic",
                    StartFranchiseSeasonId = spec.Home ? homeId : awayId,
                    StartDown = spec.Down,
                    StartDistance = spec.Dist,
                    StatYardage = spec.Yds,
                    StartYardsToEndzone = spec.Yte,
                    PeriodNumber = spec.Period
                });
            }

            await FootballDataContext.SaveChangesAsync();
            return (competitionId, homeId, awayId);
        }

        #endregion

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

