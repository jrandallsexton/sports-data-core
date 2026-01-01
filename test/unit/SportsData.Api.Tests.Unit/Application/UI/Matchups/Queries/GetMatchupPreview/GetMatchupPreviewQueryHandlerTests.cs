using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Matchups.Queries.GetMatchupPreview;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Matchups.Queries.GetMatchupPreview;

public class GetMatchupPreviewQueryHandlerTests : ApiTestBase<GetMatchupPreviewQueryHandler>
{
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
        var preview = new Infrastructure.Data.Entities.MatchupPreview
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

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetMatchupForPreview(contestId))
            .ReturnsAsync((MatchupForPreviewDto?)null);

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

        var preview = new Infrastructure.Data.Entities.MatchupPreview
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

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetMatchupForPreview(contestId))
            .ReturnsAsync(canonicalData);

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
    }
}
