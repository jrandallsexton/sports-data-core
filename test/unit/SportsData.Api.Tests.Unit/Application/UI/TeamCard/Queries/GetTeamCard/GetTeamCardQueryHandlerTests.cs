using FluentAssertions;
using SportsData.Api.Application.Common.Enums;

using Moq;

using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.TeamCard.Queries.GetTeamCard;

public class GetTeamCardQueryHandlerTests : UnitTestBase<GetTeamCardQueryHandler>
{
    private readonly Mock<IProvideCanonicalData> _canonicalDataProviderMock;

    public GetTeamCardQueryHandlerTests()
    {
        _canonicalDataProviderMock = Mocker.GetMock<IProvideCanonicalData>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenTeamCardExists()
    {
        // Arrange
        var teamCard = new TeamCardDto
        {
            FranchiseSeasonId = Guid.NewGuid(),
            Name = "Alabama Crimson Tide",
            ShortName = "Alabama",
            Slug = "alabama",
            OverallRecord = "10-2",
            ConferenceRecord = "6-2",
            ColorPrimary = "#9E1B32",
            ColorSecondary = "#FFFFFF",
            LogoUrl = "http://logo.url",
            HelmetUrl = "http://helmet.url",
            Location = "Tuscaloosa, AL",
            StadiumName = "Bryant-Denny Stadium",
            StadiumCapacity = 100077
        };

        var query = new GetTeamCardQuery
        {
            Sport = "football",
            League = "ncaaf",
            Slug = "alabama",
            SeasonYear = 2025
        };

        _canonicalDataProviderMock
            .Setup(x => x.GetTeamCard(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamCard);

        var handler = Mocker.CreateInstance<GetTeamCardQueryHandler>();

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Alabama Crimson Tide");
        result.Value.Slug.Should().Be("alabama");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenTeamCardDoesNotExist()
    {
        // Arrange
        var query = new GetTeamCardQuery
        {
            Sport = "football",
            League = "ncaaf",
            Slug = "nonexistent",
            SeasonYear = 2025
        };

        _canonicalDataProviderMock
            .Setup(x => x.GetTeamCard(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeamCardDto?)null);

        var handler = Mocker.CreateInstance<GetTeamCardQueryHandler>();

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenExceptionThrown()
    {
        // Arrange
        var query = new GetTeamCardQuery
        {
            Sport = "football",
            League = "ncaaf",
            Slug = "alabama",
            SeasonYear = 2025
        };

        _canonicalDataProviderMock
            .Setup(x => x.GetTeamCard(query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var handler = Mocker.CreateInstance<GetTeamCardQueryHandler>();

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
    }
}
