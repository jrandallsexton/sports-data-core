using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.TeamCard.Queries.GetTeamCard;

public class GetTeamCardQueryHandlerTests : UnitTestBase<GetTeamCardQueryHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;

    public GetTeamCardQueryHandlerTests()
    {
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);
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
            League = "ncaa",
            Slug = "alabama",
            SeasonYear = 2025
        };

        _franchiseClientMock
            .Setup(x => x.GetTeamCard("alabama", 2025, It.IsAny<CancellationToken>()))
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
            League = "ncaa",
            Slug = "nonexistent",
            SeasonYear = 2025
        };

        _franchiseClientMock
            .Setup(x => x.GetTeamCard("nonexistent", 2025, It.IsAny<CancellationToken>()))
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
            League = "ncaa",
            Slug = "alabama",
            SeasonYear = 2025
        };

        _franchiseClientMock
            .Setup(x => x.GetTeamCard("alabama", 2025, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("HTTP error"));

        var handler = Mocker.CreateInstance<GetTeamCardQueryHandler>();

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
    }
}
