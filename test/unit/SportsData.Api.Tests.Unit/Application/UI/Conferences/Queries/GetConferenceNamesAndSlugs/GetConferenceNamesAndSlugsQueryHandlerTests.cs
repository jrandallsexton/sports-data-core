using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Conferences.Queries.GetConferenceNamesAndSlugs;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Conferences.Queries.GetConferenceNamesAndSlugs;

public class GetConferenceNamesAndSlugsQueryHandlerTests : ApiTestBase<GetConferenceNamesAndSlugsQueryHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;

    public GetConferenceNamesAndSlugsQueryHandlerTests()
    {
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConferences_WhenDataExists()
    {
        // Arrange
        var expectedConferences = new List<ConferenceDivisionNameAndSlugDto>
        {
            new() { ShortName = "SEC", Slug = "sec", Division = "FBS" },
            new() { ShortName = "ACC", Slug = "acc", Division = "FBS" }
        };

        _franchiseClientMock
            .Setup(x => x.GetConferenceNamesAndSlugs(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConferences);

        var sut = Mocker.CreateInstance<GetConferenceNamesAndSlugsQueryHandler>();
        var query = new GetConferenceNamesAndSlugsQuery();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].ShortName.Should().Be("SEC");
        result.Value[0].Slug.Should().Be("sec");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseProvidedSeasonYear_WhenSpecified()
    {
        // Arrange
        var seasonYear = 2024;
        var expectedConferences = new List<ConferenceDivisionNameAndSlugDto>();

        _franchiseClientMock
            .Setup(x => x.GetConferenceNamesAndSlugs(seasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConferences);

        var sut = Mocker.CreateInstance<GetConferenceNamesAndSlugsQueryHandler>();
        var query = new GetConferenceNamesAndSlugsQuery { SeasonYear = seasonYear };

        // Act
        await sut.ExecuteAsync(query);

        // Assert
        _franchiseClientMock
            .Verify(x => x.GetConferenceNamesAndSlugs(seasonYear, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseCurrentYear_WhenSeasonYearNotSpecified()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var expectedConferences = new List<ConferenceDivisionNameAndSlugDto>();

        _franchiseClientMock
            .Setup(x => x.GetConferenceNamesAndSlugs(currentYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConferences);

        var sut = Mocker.CreateInstance<GetConferenceNamesAndSlugsQueryHandler>();
        var query = new GetConferenceNamesAndSlugsQuery();

        // Act
        await sut.ExecuteAsync(query);

        // Assert
        _franchiseClientMock
            .Verify(x => x.GetConferenceNamesAndSlugs(currentYear, It.IsAny<CancellationToken>()), Times.Once);
    }
}
