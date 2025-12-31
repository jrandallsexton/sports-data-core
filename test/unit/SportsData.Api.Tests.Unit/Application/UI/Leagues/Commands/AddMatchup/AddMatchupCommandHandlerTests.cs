using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.UI.Leagues.Commands.AddMatchup;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Application;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

using Xunit;

using League = SportsData.Api.Application.League;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.AddMatchup;

public class AddMatchupCommandHandlerTests : ApiTestBase<AddMatchupCommandHandler>
{
    private readonly Mock<IProvideCanonicalData> _canonicalDataProviderMock;
    private readonly Mock<IEventBus> _eventBusMock;

    public AddMatchupCommandHandlerTests()
    {
        _canonicalDataProviderMock = Mocker.GetMock<IProvideCanonicalData>();
        _eventBusMock = Mocker.GetMock<IEventBus>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLeagueNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var handler = Mocker.CreateInstance<AddMatchupCommandHandler>();
        var command = CreateCommand();

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        var failure = result as Failure<Guid>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.LeagueId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsNotCommissioner_ReturnsUnauthorizedFailure()
    {
        // Arrange
        var commissionerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();

        var league = CreateLeague(commissionerId);
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<AddMatchupCommandHandler>();
        var command = CreateCommand(leagueId: league.Id, userId: differentUserId);

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Unauthorized);
        var failure = result as Failure<Guid>;
        failure!.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("commissioner"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenMatchupAlreadyExists_ReturnsValidationFailure()
    {
        // Arrange
        var commissionerId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();

        var league = CreateLeague(commissionerId);
        DataContext.PickemGroups.Add(league);

        var groupWeek = CreateGroupWeek(league.Id, seasonWeekId);
        DataContext.PickemGroupWeeks.Add(groupWeek);

        var existingMatchup = CreateMatchup(league.Id, contestId, seasonWeekId);
        DataContext.PickemGroupMatchups.Add(existingMatchup);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<AddMatchupCommandHandler>();
        var command = CreateCommand(leagueId: league.Id, contestId: contestId, userId: commissionerId);

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        var failure = result as Failure<Guid>;
        failure!.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("already exists"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenContestNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var commissionerId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var league = CreateLeague(commissionerId);
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupByContestId(contestId))
            .ReturnsAsync((Matchup?)null);

        var handler = Mocker.CreateInstance<AddMatchupCommandHandler>();
        var command = CreateCommand(leagueId: league.Id, contestId: contestId, userId: commissionerId);

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        var failure = result as Failure<Guid>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.ContestId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenGroupWeekDoesNotExist_CreatesGroupWeekAndMatchup()
    {
        // Arrange
        var commissionerId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();

        var league = CreateLeague(commissionerId);
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        var matchupData = CreateMatchupData(contestId, seasonWeekId);
        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupByContestId(contestId))
            .ReturnsAsync(matchupData);

        var handler = Mocker.CreateInstance<AddMatchupCommandHandler>();
        var command = CreateCommand(leagueId: league.Id, contestId: contestId, userId: commissionerId);

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var createdGroupWeek = await DataContext.PickemGroupWeeks
            .FirstOrDefaultAsync(w => w.GroupId == league.Id && w.SeasonWeekId == seasonWeekId);
        createdGroupWeek.Should().NotBeNull();
        createdGroupWeek!.SeasonYear.Should().Be(matchupData.SeasonYear);
        createdGroupWeek.SeasonWeek.Should().Be(matchupData.SeasonWeek);

        var createdMatchup = await DataContext.PickemGroupMatchups
            .FirstOrDefaultAsync(m => m.ContestId == contestId);
        createdMatchup.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenGroupWeekExists_UsesExistingGroupWeek()
    {
        // Arrange
        var commissionerId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();

        var league = CreateLeague(commissionerId);
        DataContext.PickemGroups.Add(league);

        var existingGroupWeek = CreateGroupWeek(league.Id, seasonWeekId);
        DataContext.PickemGroupWeeks.Add(existingGroupWeek);
        await DataContext.SaveChangesAsync();

        var matchupData = CreateMatchupData(contestId, seasonWeekId);
        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupByContestId(contestId))
            .ReturnsAsync(matchupData);

        var handler = Mocker.CreateInstance<AddMatchupCommandHandler>();
        var command = CreateCommand(leagueId: league.Id, contestId: contestId, userId: commissionerId);

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var groupWeekCount = await DataContext.PickemGroupWeeks
            .CountAsync(w => w.GroupId == league.Id && w.SeasonWeekId == seasonWeekId);
        groupWeekCount.Should().Be(1); // Should not create a duplicate
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_PersistsMatchupWithCorrectData()
    {
        // Arrange
        var commissionerId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();

        var league = CreateLeague(commissionerId);
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        var matchupData = CreateMatchupData(contestId, seasonWeekId);
        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupByContestId(contestId))
            .ReturnsAsync(matchupData);

        var handler = Mocker.CreateInstance<AddMatchupCommandHandler>();
        var command = CreateCommand(leagueId: league.Id, contestId: contestId, userId: commissionerId);

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var createdMatchup = await DataContext.PickemGroupMatchups
            .FirstOrDefaultAsync(m => m.ContestId == contestId);

        createdMatchup.Should().NotBeNull();
        createdMatchup!.GroupId.Should().Be(league.Id);
        createdMatchup.ContestId.Should().Be(contestId);
        createdMatchup.SeasonWeekId.Should().Be(seasonWeekId);
        createdMatchup.SeasonYear.Should().Be(matchupData.SeasonYear);
        createdMatchup.SeasonWeek.Should().Be(matchupData.SeasonWeek);
        createdMatchup.Headline.Should().Be(matchupData.Headline);
        createdMatchup.AwayRank.Should().Be(matchupData.AwayRank);
        createdMatchup.HomeRank.Should().Be(matchupData.HomeRank);
        createdMatchup.AwayWins.Should().Be(matchupData.AwayWins);
        createdMatchup.AwayLosses.Should().Be(matchupData.AwayLosses);
        createdMatchup.HomeWins.Should().Be(matchupData.HomeWins);
        createdMatchup.HomeLosses.Should().Be(matchupData.HomeLosses);
        createdMatchup.CreatedBy.Should().Be(commissionerId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_PublishesPickemGroupMatchupAddedEvent()
    {
        // Arrange
        var commissionerId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();

        var league = CreateLeague(commissionerId);
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        var matchupData = CreateMatchupData(contestId, seasonWeekId);
        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupByContestId(contestId))
            .ReturnsAsync(matchupData);

        var handler = Mocker.CreateInstance<AddMatchupCommandHandler>();
        var command = CreateCommand(leagueId: league.Id, contestId: contestId, userId: commissionerId);

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _eventBusMock.Verify(
            x => x.Publish(
                It.Is<PickemGroupMatchupAdded>(e =>
                    e.GroupId == league.Id &&
                    e.ContestId == contestId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsMatchupId()
    {
        // Arrange
        var commissionerId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();

        var league = CreateLeague(commissionerId);
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        var matchupData = CreateMatchupData(contestId, seasonWeekId);
        _canonicalDataProviderMock
            .Setup(x => x.GetMatchupByContestId(contestId))
            .ReturnsAsync(matchupData);

        var handler = Mocker.CreateInstance<AddMatchupCommandHandler>();
        var command = CreateCommand(leagueId: league.Id, contestId: contestId, userId: commissionerId);

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);

        var createdMatchup = await DataContext.PickemGroupMatchups
            .FirstOrDefaultAsync(m => m.Id == result.Value);
        createdMatchup.Should().NotBeNull();
    }

    #region Helper Methods

    private static AddMatchupCommand CreateCommand(
        Guid? leagueId = null,
        Guid? contestId = null,
        Guid? userId = null)
    {
        return new AddMatchupCommand
        {
            LeagueId = leagueId ?? Guid.NewGuid(),
            ContestId = contestId ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid()
        };
    }

    private static PickemGroup CreateLeague(Guid commissionerId)
    {
        return new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test League",
            CommissionerUserId = commissionerId,
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            IsPublic = false,
            UseConfidencePoints = false,
            CreatedBy = commissionerId,
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static PickemGroupWeek CreateGroupWeek(Guid groupId, Guid seasonWeekId)
    {
        return new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonWeekId = seasonWeekId,
            SeasonYear = 2025,
            SeasonWeek = 15,
            IsNonStandardWeek = false,
            AreMatchupsGenerated = true
        };
    }

    private static PickemGroupMatchup CreateMatchup(Guid groupId, Guid contestId, Guid seasonWeekId)
    {
        return new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            SeasonWeekId = seasonWeekId,
            SeasonYear = 2025,
            SeasonWeek = 15,
            StartDateUtc = DateTime.UtcNow.AddDays(1),
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static Matchup CreateMatchupData(Guid contestId, Guid seasonWeekId)
    {
        return new Matchup
        {
            ContestId = contestId,
            SeasonWeekId = seasonWeekId,
            SeasonYear = 2025,
            SeasonWeek = 15,
            Headline = "CFP Semifinal",
            StartDateUtc = DateTime.UtcNow.AddDays(1),
            Status = "scheduled",
            AwaySlug = "ohio-state",
            AwayAbbreviation = "OSU",
            AwayRank = 1,
            AwayWins = 12,
            AwayLosses = 1,
            AwayConferenceWins = 8,
            AwayConferenceLosses = 1,
            HomeSlug = "texas",
            HomeAbbreviation = "TEX",
            HomeRank = 4,
            HomeWins = 11,
            HomeLosses = 2,
            HomeConferenceWins = 7,
            HomeConferenceLosses = 2,
            Spread = "TEX -3.5",
            AwaySpread = 3.5,
            HomeSpread = -3.5,
            OverUnder = 52.5
        };
    }

    #endregion
}
