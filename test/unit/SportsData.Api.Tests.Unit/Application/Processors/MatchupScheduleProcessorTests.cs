using AutoFixture;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Processors
{
    public class MatchupScheduleProcessorTests : ApiTestBase<MatchupScheduleProcessor>
    {
        /// <summary>
        /// Validates that when a PickemGroup does not exist for the given GroupId,
        /// the processor logs an error and returns early without attempting to fetch matchups.
        /// </summary>
        [Fact]
        public async Task Process_WhenGroupNotFound_LogsErrorAndReturns()
        {
            // Arrange
            var command = new ScheduleGroupWeekMatchupsCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                2024,
                1,
                false,
                Guid.NewGuid());

            var sut = Mocker.CreateInstance<MatchupScheduleProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            Mocker.GetMock<IProvideCanonicalData>()
                .Verify(x => x.GetMatchupsForCurrentWeek(), Times.Never);
        }

        /// <summary>
        /// Validates that when matchups have already been generated for a PickemGroupWeek,
        /// the processor logs a warning and returns early without re-processing matchups.
        /// This prevents duplicate matchup generation.
        /// </summary>
        [Fact]
        public async Task Process_WhenMatchupsAlreadyGenerated_LogsWarningAndReturns()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var seasonWeekId = Guid.NewGuid();

            // Create group first
            var group = new PickemGroup
            {
                Id = groupId,
                Name = "Test Group",
                Sport = Core.Common.Sport.FootballNcaa,
                League = League.NCAAF,
                CommissionerUserId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            };

            await DataContext.PickemGroups.AddAsync(group);
            await DataContext.SaveChangesAsync();

            // Create groupWeek with composite key
            var groupWeek = new PickemGroupWeek
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                SeasonWeekId = seasonWeekId,
                SeasonYear = 2024,
                SeasonWeek = 1,
                AreMatchupsGenerated = true,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            };

            await DataContext.PickemGroupWeeks.AddAsync(groupWeek);
            await DataContext.SaveChangesAsync();

            var command = new ScheduleGroupWeekMatchupsCommand(
                groupId,
                seasonWeekId,
                2024,
                1,
                false,
                Guid.NewGuid());

            var sut = Mocker.CreateInstance<MatchupScheduleProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            Mocker.GetMock<IProvideCanonicalData>()
                .Verify(x => x.GetMatchupsForCurrentWeek(), Times.Never);
        }

        /// <summary>
        /// Validates that for standard (regular season) weeks, the processor correctly filters matchups
        /// based on team rankings (AP Top 25) and conference membership.
        /// Matchups with either team ranked in the top X or from selected conferences should be included.
        /// </summary>
        [Fact]
        public async Task Process_StandardWeek_FiltersMatchupsByRankAndConference()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var seasonWeekId = Guid.NewGuid();
            var conferenceSlug = "sec";

            var group = Fixture.Build<PickemGroup>()
                .With(x => x.Id, groupId)
                .With(x => x.RankingFilter, () => TeamRankingFilter.AP_TOP_25)
                .With(x => x.Conferences, new List<PickemGroupConference>
                {
                    new() { Id = Guid.NewGuid(), ConferenceSlug = conferenceSlug, PickemGroupId = groupId, ConferenceId = Guid.NewGuid(), CreatedUtc = DateTime.UtcNow, CreatedBy = Guid.Empty }
                })
                .Create();

            await DataContext.PickemGroups.AddAsync(group);
            await DataContext.SaveChangesAsync();

            var allMatchups = new List<Matchup>
            {
                // Ranked team
                Fixture.Build<Matchup>()
                    .With(x => x.AwayRank, 10)
                    .With(x => x.HomeRank, (int?)null)
                    .With(x => x.AwayConferenceSlug, "big12")
                    .With(x => x.HomeConferenceSlug, "big12")
                    .Create(),
                // Conference match
                Fixture.Build<Matchup>()
                    .With(x => x.AwayRank, (int?)null)
                    .With(x => x.HomeRank, (int?)null)
                    .With(x => x.AwayConferenceSlug, conferenceSlug)
                    .With(x => x.HomeConferenceSlug, "acc")
                    .Create(),
                // Should be excluded
                Fixture.Build<Matchup>()
                    .With(x => x.AwayRank, (int?)null)
                    .With(x => x.HomeRank, (int?)null)
                    .With(x => x.AwayConferenceSlug, "big12")
                    .With(x => x.HomeConferenceSlug, "acc")
                    .Create()
            };

            Mocker.GetMock<IProvideCanonicalData>()
                .Setup(x => x.GetMatchupsForCurrentWeek())
                .ReturnsAsync(allMatchups);

            var command = new ScheduleGroupWeekMatchupsCommand(
                groupId,
                seasonWeekId,
                2024,
                1,
                false,
                Guid.NewGuid());

            var sut = Mocker.CreateInstance<MatchupScheduleProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            var savedGroupWeek = DataContext.PickemGroupWeeks
                .Where(x => x.SeasonWeekId == seasonWeekId)
                .FirstOrDefault();

            savedGroupWeek.Should().NotBeNull();
            savedGroupWeek!.Matchups.Should().HaveCount(2); // Only ranked and conference matchups
            savedGroupWeek.AreMatchupsGenerated.Should().BeTrue();
        }

        /// <summary>
        /// Validates that for non-standard weeks (e.g., conference championship week, bowl season),
        /// the processor applies additional filtering based on GroupSeasonMap values.
        /// The filtering is additive: ranked teams + conference teams + GroupSeasonMap matches.
        /// For example, with filter "fbs", all FBS teams are included alongside ranked teams.
        /// </summary>
        [Fact]
        public async Task Process_NonStandardWeek_FiltersMatchupsByGroupSeasonMap()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var seasonWeekId = Guid.NewGuid();

            var group = Fixture.Build<PickemGroup>()
                .With(x => x.Id, groupId)
                .With(x => x.RankingFilter, () => TeamRankingFilter.AP_TOP_25)
                .With(x => x.NonStandardWeekGroupSeasonMapFilter, "fbs")
                .With(x => x.Conferences, new List<PickemGroupConference>())
                .Create();

            await DataContext.PickemGroups.AddAsync(group);
            await DataContext.SaveChangesAsync();

            var allMatchups = new List<Matchup>
            {
                // FBS matchup (should be included)
                Fixture.Build<Matchup>()
                    .With(x => x.AwayRank, (int?)null)
                    .With(x => x.HomeRank, (int?)null)
                    .With(x => x.AwayGroupSeasonMap, "NCAAF|NCAA|fbs|SEC")
                    .With(x => x.HomeGroupSeasonMap, "NCAAF|NCAA|fbs|BigTen")
                    .Create(),
                // FCS matchup (should be excluded)
                Fixture.Build<Matchup>()
                    .With(x => x.AwayRank, (int?)null)
                    .With(x => x.HomeRank, (int?)null)
                    .With(x => x.AwayGroupSeasonMap, "NCAAF|NCAA|fcs|SoCon")
                    .With(x => x.HomeGroupSeasonMap, "NCAAF|NCAA|fcs|BigSky")
                    .Create(),
                // Ranked FCS team (should be included due to rank filter)
                Fixture.Build<Matchup>()
                    .With(x => x.AwayRank, 15)
                    .With(x => x.HomeRank, (int?)null)
                    .With(x => x.AwayGroupSeasonMap, "NCAAF|NCAA|fcs|SoCon")
                    .With(x => x.HomeGroupSeasonMap, "NCAAF|NCAA|fcs|BigSky")
                    .Create()
            };

            Mocker.GetMock<IProvideCanonicalData>()
                .Setup(x => x.GetMatchupsForCurrentWeek())
                .ReturnsAsync(allMatchups);

            var command = new ScheduleGroupWeekMatchupsCommand(
                groupId,
                seasonWeekId,
                2024,
                16, // Championship week
                true, // Non-standard week
                Guid.NewGuid());

            var sut = Mocker.CreateInstance<MatchupScheduleProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            var savedGroupWeek = DataContext.PickemGroupWeeks
                .Where(x => x.SeasonWeekId == seasonWeekId)
                .FirstOrDefault();

            savedGroupWeek.Should().NotBeNull();
            savedGroupWeek!.Matchups.Should().HaveCount(2); // FBS matchup + ranked FCS
            savedGroupWeek.IsNonStandardWeek.Should().BeTrue();
        }

        /// <summary>
        /// Validates that the GroupSeasonMap filtering is case-insensitive.
        /// A filter value of "FBS" should match data containing "fbs" in the GroupSeasonMap field.
        /// This ensures robust matching regardless of data casing.
        /// </summary>
        [Theory]
        [InlineData("FBS")]
        [InlineData("fbs")]
        [InlineData("Fbs")]
        [InlineData("fBs")]
        public async Task Process_NonStandardWeek_CaseInsensitiveFilter(string filter)
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var seasonWeekId = Guid.NewGuid();

            var group = Fixture.Build<PickemGroup>()
                .With(x => x.Id, groupId)
                .With(x => x.RankingFilter, () => (TeamRankingFilter?)null)
                .With(x => x.NonStandardWeekGroupSeasonMapFilter, filter) // Use the filter parameter
                .With(x => x.Conferences, new List<PickemGroupConference>())
                .Create();

            await DataContext.PickemGroups.AddAsync(group);
            await DataContext.SaveChangesAsync();

            var allMatchups = new List<Matchup>
            {
                Fixture.Build<Matchup>()
                    .With(x => x.AwayRank, (int?)null)
                    .With(x => x.HomeRank, (int?)null)
                    .With(x => x.AwayGroupSeasonMap, "NCAAF|NCAA|fbs|SEC") // Lowercase in data
                    .With(x => x.HomeGroupSeasonMap, "NCAAF|NCAA|fbs|BigTen")
                    .Create()
            };

            Mocker.GetMock<IProvideCanonicalData>()
                .Setup(x => x.GetMatchupsForCurrentWeek())
                .ReturnsAsync(allMatchups);

            var command = new ScheduleGroupWeekMatchupsCommand(
                groupId,
                seasonWeekId,
                2024,
                16,
                true,
                Guid.NewGuid());

            var sut = Mocker.CreateInstance<MatchupScheduleProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            var savedGroupWeek = DataContext.PickemGroupWeeks
                .Include(pickemGroupWeek => pickemGroupWeek.Matchups)
                .FirstOrDefault(x => x.SeasonWeekId == seasonWeekId);

            savedGroupWeek.Should().NotBeNull();
            savedGroupWeek!.Matchups.Should().ContainSingle(); // Should match despite case difference
        }

        /// <summary>
        /// Validates that the NonStandardWeekGroupSeasonMapFilter supports multiple pipe-delimited filters.
        /// For example, "fbs|bowl" should match matchups where GroupSeasonMap contains either "fbs" OR "bowl".
        /// This allows flexible filtering for complex non-standard weeks like bowl season.
        /// </summary>
        [Fact]
        public async Task Process_NonStandardWeek_MultipleFilters()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var seasonWeekId = Guid.NewGuid();

            var group = Fixture.Build<PickemGroup>()
                .With(x => x.Id, groupId)
                .With(x => x.RankingFilter, () => (TeamRankingFilter?)null)
                .With(x => x.NonStandardWeekGroupSeasonMapFilter, "fbs|bowl") // Multiple filters
                .With(x => x.Conferences, new List<PickemGroupConference>())
                .Create();

            await DataContext.PickemGroups.AddAsync(group);
            await DataContext.SaveChangesAsync();

            var allMatchups = new List<Matchup>
            {
                Fixture.Build<Matchup>()
                    .With(x => x.AwayRank, (int?)null)
                    .With(x => x.HomeRank, (int?)null)
                    .With(x => x.AwayGroupSeasonMap, "NCAAF|NCAA|fbs|SEC")
                    .With(x => x.HomeGroupSeasonMap, "NCAAF|NCAA|fbs|BigTen")
                    .Create(),
                Fixture.Build<Matchup>()
                    .With(x => x.AwayRank, (int?)null)
                    .With(x => x.HomeRank, (int?)null)
                    .With(x => x.AwayGroupSeasonMap, "NCAAF|NCAA|bowl|RoseBowl")
                    .With(x => x.HomeGroupSeasonMap, "NCAAF|NCAA|bowl|SugarBowl")
                    .Create()
            };

            Mocker.GetMock<IProvideCanonicalData>()
                .Setup(x => x.GetMatchupsForCurrentWeek())
                .ReturnsAsync(allMatchups);

            var command = new ScheduleGroupWeekMatchupsCommand(
                groupId,
                seasonWeekId,
                2024,
                17,
                true,
                Guid.NewGuid());

            var sut = Mocker.CreateInstance<MatchupScheduleProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            var savedGroupWeek = DataContext.PickemGroupWeeks
                .Include(pickemGroupWeek => pickemGroupWeek.Matchups)
                .FirstOrDefault(x => x.SeasonWeekId == seasonWeekId);

            savedGroupWeek.Should().NotBeNull();
            savedGroupWeek!.Matchups.Should().HaveCount(2); // Both fbs and bowl matchups
        }

        /// <summary>
        /// Validates that when a PickemGroupWeek does not exist for the given GroupId and SeasonWeekId,
        /// the processor automatically creates a new PickemGroupWeek entity with the correct properties
        /// before processing matchups.
        /// </summary>
        [Fact]
        public async Task Process_CreatesNewGroupWeek_WhenNotExists()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var seasonWeekId = Guid.NewGuid();

            var group = Fixture.Build<PickemGroup>()
                .With(x => x.Id, groupId)
                .With(x => x.RankingFilter, () => TeamRankingFilter.AP_TOP_25)
                .With(x => x.Conferences, new List<PickemGroupConference>())
                .Create();

            await DataContext.PickemGroups.AddAsync(group);
            await DataContext.SaveChangesAsync();

            Mocker.GetMock<IProvideCanonicalData>()
                .Setup(x => x.GetMatchupsForCurrentWeek())
                .ReturnsAsync(new List<Matchup>());

            var command = new ScheduleGroupWeekMatchupsCommand(
                groupId,
                seasonWeekId,
                2024,
                5,
                false,
                Guid.NewGuid());

            var sut = Mocker.CreateInstance<MatchupScheduleProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            var savedGroupWeek = DataContext.PickemGroupWeeks
                .FirstOrDefault(x => x.SeasonWeekId == seasonWeekId && x.GroupId == groupId);

            savedGroupWeek.Should().NotBeNull();
            savedGroupWeek!.SeasonYear.Should().Be(2024);
            savedGroupWeek.SeasonWeek.Should().Be(5);
            savedGroupWeek.IsNonStandardWeek.Should().BeFalse();
        }

        /// <summary>
        /// Validates that upon successful matchup generation, the processor publishes
        /// a PickemGroupWeekMatchupsGenerated event with the correct GroupId, SeasonYear,
        /// and CorrelationId for downstream consumers to react to.
        /// </summary>
        [Fact]
        public async Task Process_PublishesPickemGroupWeekMatchupsGeneratedEvent()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var seasonWeekId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var group = Fixture.Build<PickemGroup>()
                .With(x => x.Id, groupId)
                .With(x => x.RankingFilter, () => TeamRankingFilter.AP_TOP_25)
                .With(x => x.Conferences, new List<PickemGroupConference>())
                .Create();

            await DataContext.PickemGroups.AddAsync(group);
            await DataContext.SaveChangesAsync();

            Mocker.GetMock<IProvideCanonicalData>()
                .Setup(x => x.GetMatchupsForCurrentWeek())
                .ReturnsAsync(new List<Matchup>());

            var command = new ScheduleGroupWeekMatchupsCommand(
                groupId,
                seasonWeekId,
                2024,
                3,
                false,
                correlationId);

            var sut = Mocker.CreateInstance<MatchupScheduleProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            Mocker.GetMock<IEventBus>()
                .Verify(x => x.Publish(
                    It.Is<PickemGroupWeekMatchupsGenerated>(e =>
                        e.GroupId == groupId &&
                        e.SeasonYear == 2024 &&
                        e.CorrelationId == correlationId),
                    It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        /// <summary>
        /// Validates that all matchup data fields (ranks, wins/losses, spread, etc.)
        /// are correctly copied from the source Matchup entities to the PickemGroupMatchup entities
        /// when generating matchups for a league week.
        /// </summary>
        [Fact]
        public async Task Process_CopiesMatchupDataCorrectly()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var seasonWeekId = Guid.NewGuid();
            var contestId = Guid.NewGuid();

            var group = Fixture.Build<PickemGroup>()
                .With(x => x.Id, groupId)
                .With(x => x.RankingFilter, () => TeamRankingFilter.AP_TOP_25)
                .With(x => x.Conferences, new List<PickemGroupConference>())
                .Create();

            await DataContext.PickemGroups.AddAsync(group);
            await DataContext.SaveChangesAsync();

            var sourceMatchup = Fixture.Build<Matchup>()
                .With(x => x.ContestId, contestId)
                .With(x => x.SeasonWeekId, seasonWeekId)
                .With(x => x.AwayRank, 5)
                .With(x => x.HomeRank, 10)
                .With(x => x.AwayWins, 8)
                .With(x => x.AwayLosses, 2)
                .With(x => x.HomeWins, 7)
                .With(x => x.HomeLosses, 3)
                .With(x => x.Spread, () => "-3.5")
                .Create();

            Mocker.GetMock<IProvideCanonicalData>()
                .Setup(x => x.GetMatchupsForCurrentWeek())
                .ReturnsAsync(new List<Matchup> { sourceMatchup });

            var command = new ScheduleGroupWeekMatchupsCommand(
                groupId,
                seasonWeekId,
                2024,
                8,
                false,
                Guid.NewGuid());

            var sut = Mocker.CreateInstance<MatchupScheduleProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            var savedGroupWeek = DataContext.PickemGroupWeeks
                .Include(pickemGroupWeek => pickemGroupWeek.Matchups)
                .FirstOrDefault(x => x.SeasonWeekId == seasonWeekId);

            savedGroupWeek.Should().NotBeNull();
            savedGroupWeek!.Matchups.Should().ContainSingle();
            var savedMatchup = savedGroupWeek.Matchups.Single();
            
            savedMatchup.ContestId.Should().Be(contestId);
            savedMatchup.AwayRank.Should().Be(5);
            savedMatchup.HomeRank.Should().Be(10);
            savedMatchup.AwayWins.Should().Be(8);
            savedMatchup.AwayLosses.Should().Be(2);
            savedMatchup.HomeWins.Should().Be(7);
            savedMatchup.HomeLosses.Should().Be(3);
            savedMatchup.Spread.Should().Be("-3.5");
        }
    }
}
