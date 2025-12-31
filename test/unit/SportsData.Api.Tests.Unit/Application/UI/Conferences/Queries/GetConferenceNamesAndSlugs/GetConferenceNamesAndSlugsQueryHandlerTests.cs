using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Conferences.Queries.GetConferenceNamesAndSlugs;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Conferences.Queries.GetConferenceNamesAndSlugs;

public class GetConferenceNamesAndSlugsQueryHandlerTests : ApiTestBase<GetConferenceNamesAndSlugsQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnConferences_WhenDataExists()
    {
        // Arrange
        var expectedConferences = new List<ConferenceDivisionNameAndSlugDto>
        {
            new() { ShortName = "SEC", Slug = "sec", Division = "FBS" },
            new() { ShortName = "ACC", Slug = "acc", Division = "FBS" }
        };

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetConferenceNamesAndSlugsForSeasonYear(It.IsAny<int>()))
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

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetConferenceNamesAndSlugsForSeasonYear(seasonYear))
            .ReturnsAsync(expectedConferences);

        var sut = Mocker.CreateInstance<GetConferenceNamesAndSlugsQueryHandler>();
        var query = new GetConferenceNamesAndSlugsQuery { SeasonYear = seasonYear };

        // Act
        await sut.ExecuteAsync(query);

        // Assert
        Mocker.GetMock<IProvideCanonicalData>()
            .Verify(x => x.GetConferenceNamesAndSlugsForSeasonYear(seasonYear), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseCurrentYear_WhenSeasonYearNotSpecified()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var expectedConferences = new List<ConferenceDivisionNameAndSlugDto>();

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetConferenceNamesAndSlugsForSeasonYear(currentYear))
            .ReturnsAsync(expectedConferences);

        var sut = Mocker.CreateInstance<GetConferenceNamesAndSlugsQueryHandler>();
        var query = new GetConferenceNamesAndSlugsQuery();

        // Act
        await sut.ExecuteAsync(query);

        // Assert
        Mocker.GetMock<IProvideCanonicalData>()
            .Verify(x => x.GetConferenceNamesAndSlugsForSeasonYear(currentYear), Times.Once);
    }
}
