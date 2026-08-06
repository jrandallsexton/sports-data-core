using FluentAssertions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Matchups.Queries.GetMatchupPreview;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Matchups.Queries.GetMatchupPreview;

public class GetMatchupPreviewQueryHandlerTests : ApiTestBase<GetMatchupPreviewQueryHandler>
{
    private readonly Mock<IProvideContests> _contestClientMock = new();

    public GetMatchupPreviewQueryHandlerTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);
    }
    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenPreviewDoesNotExist()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var sut = Mocker.CreateInstance<GetMatchupPreviewQueryHandler>();
        var query = new GetMatchupPreviewQuery { ContestId = contestId };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenCanonicalDataNotFound()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var preview = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            CreatedUtc = DateTime.UtcNow,
            Overview = "Test overview",
            Analysis = "Test analysis",
            Prediction = "Test prediction"
        };
        await DataContext.MatchupPreviews.AddAsync(preview);
        await DataContext.SaveChangesAsync();

        _contestClientMock
            .Setup(x => x.GetMatchupForPreview(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<MatchupForPreviewDto>(default!, ResultStatus.NotFound, []));

        var sut = Mocker.CreateInstance<GetMatchupPreviewQueryHandler>();
        var query = new GetMatchupPreviewQuery { ContestId = contestId };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnPreview_WhenDataExists()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();
        var homeFranchiseSeasonId = Guid.NewGuid();

        var preview = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            CreatedUtc = DateTime.UtcNow,
            Overview = "Test overview",
            Analysis = "Test analysis",
            Prediction = "Test prediction",
            PredictedStraightUpWinner = awayFranchiseSeasonId,
            PredictedSpreadWinner = homeFranchiseSeasonId,
            AwayScore = 24,
            HomeScore = 21
        };
        await DataContext.MatchupPreviews.AddAsync(preview);
        await DataContext.SaveChangesAsync();

        var canonicalData = new MatchupForPreviewDto
        {
            ContestId = contestId,
            AwayFranchiseSeasonId = awayFranchiseSeasonId,
            HomeFranchiseSeasonId = homeFranchiseSeasonId,
            Away = "Away Team",
            Home = "Home Team",
            AwaySlug = "away-team",
            HomeSlug = "home-team",
            AwayConferenceSlug = "conf-a",
            HomeConferenceSlug = "conf-b",
            Venue = "Stadium",
            VenueCity = "City",
            HomeSpread = -3.5,
            OverUnder = 45.5
        };

        _contestClientMock
            .Setup(x => x.GetMatchupForPreview(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupForPreviewDto>(canonicalData));

        var sut = Mocker.CreateInstance<GetMatchupPreviewQueryHandler>();
        var query = new GetMatchupPreviewQuery { ContestId = contestId };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ContestId.Should().Be(contestId);
        result.Value.Overview.Should().Be("Test overview");
        result.Value.StraightUpWinner.Should().Be("Away Team");
        result.Value.AtsWinner.Should().Be("Home Team");
        result.Value.AwayScore.Should().Be(24);
        result.Value.HomeScore.Should().Be(21);
        // No canonical status seeded -> not completed.
        result.Value.IsContestCompleted.Should().BeFalse();
    }

    // IsContestCompleted derives from the canonical status — the admin
    // approve/reject affordances hide once the game has been played.
    [Theory]
    [InlineData("STATUS_FINAL", true)]
    [InlineData("STATUS_SCHEDULED", false)]
    [InlineData("STATUS_IN_PROGRESS", false)]
    [InlineData(null, false)]
    public async Task ExecuteAsync_SetsIsContestCompleted_FromCanonicalStatus(
        string? canonicalStatus, bool expected)
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var preview = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            CreatedUtc = DateTime.UtcNow,
            Overview = "Test overview",
            Analysis = "Test analysis",
            Prediction = "Test prediction"
        };
        await DataContext.MatchupPreviews.AddAsync(preview);
        await DataContext.SaveChangesAsync();

        var canonicalData = new MatchupForPreviewDto
        {
            ContestId = contestId,
            Status = canonicalStatus,
            Away = "Away Team",
            Home = "Home Team",
            AwaySlug = "away-team",
            HomeSlug = "home-team",
            AwayConferenceSlug = "conf-a",
            HomeConferenceSlug = "conf-b",
            Venue = "Stadium",
            VenueCity = "City"
        };

        _contestClientMock
            .Setup(x => x.GetMatchupForPreview(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupForPreviewDto>(canonicalData));

        var sut = Mocker.CreateInstance<GetMatchupPreviewQueryHandler>();
        var query = new GetMatchupPreviewQuery { ContestId = contestId };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsContestCompleted.Should().Be(expected);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesSportFromContestsLeague()
    {
        // Arrange — the contest belongs to an NFL league, so the canonical
        // client must be resolved for FootballNfl, not the NCAA fallback.
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var commissionerId = Guid.NewGuid();

        await DataContext.PickemGroups.AddAsync(new PickemGroup
        {
            Id = groupId,
            Name = "NFL League",
            CommissionerUserId = commissionerId,
            Sport = Sport.FootballNfl,
            League = League.NFL,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            SeasonYear = 2026,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = commissionerId
        });
        await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId,
            StartDateUtc = DateTime.UtcNow.AddDays(3),
            SeasonYear = 2026,
            SeasonWeek = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = commissionerId
        });
        await DataContext.MatchupPreviews.AddAsync(new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            CreatedUtc = DateTime.UtcNow,
            Overview = "Test overview",
            Analysis = "Test analysis",
            Prediction = "Test prediction"
        });
        await DataContext.SaveChangesAsync();

        _contestClientMock
            .Setup(x => x.GetMatchupForPreview(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupForPreviewDto>(new MatchupForPreviewDto
            {
                ContestId = contestId,
                Away = "Away Team",
                Home = "Home Team",
                AwaySlug = "away-team",
                HomeSlug = "home-team",
                AwayConferenceSlug = "conf-a",
                HomeConferenceSlug = "conf-b",
                Venue = "Stadium",
                VenueCity = "City"
            }));

        var sut = Mocker.CreateInstance<GetMatchupPreviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetMatchupPreviewQuery { ContestId = contestId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IContestClientFactory>()
            .Verify(x => x.Resolve(Sport.FootballNfl), Times.Once);
        Mocker.GetMock<IContestClientFactory>()
            .Verify(x => x.Resolve(Sport.FootballNcaa), Times.Never);
    }
}


