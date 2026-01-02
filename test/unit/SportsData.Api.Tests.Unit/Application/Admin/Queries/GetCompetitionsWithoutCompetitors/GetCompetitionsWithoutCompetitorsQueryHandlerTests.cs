using FluentAssertions;

using Moq;

using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutCompetitors;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Queries.GetCompetitionsWithoutCompetitors;

public class GetCompetitionsWithoutCompetitorsQueryHandlerTests : ApiTestBase<GetCompetitionsWithoutCompetitorsQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenDataProviderReturnsResults()
    {
        // Arrange
        var expectedResults = new List<CompetitionWithoutCompetitorsDto>
        {
            new() { CompetitionId = Guid.NewGuid(), CompetitionName = "Test Competition 1" },
            new() { CompetitionId = Guid.NewGuid(), CompetitionName = "Test Competition 2" }
        };

        Mocker.GetMock<IProvideCanonicalAdminData>()
            .Setup(x => x.GetCompetitionsWithoutCompetitors(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();
        var query = new GetCompetitionsWithoutCompetitorsQuery();

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<Success<List<CompetitionWithoutCompetitorsDto>>>();
        var success = (Success<List<CompetitionWithoutCompetitorsDto>>)result;
        success.Value.Should().HaveCount(2);
        success.Value.Should().BeEquivalentTo(expectedResults);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenNoResultsFound()
    {
        // Arrange
        var expectedResults = new List<CompetitionWithoutCompetitorsDto>();

        Mocker.GetMock<IProvideCanonicalAdminData>()
            .Setup(x => x.GetCompetitionsWithoutCompetitors(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();
        var query = new GetCompetitionsWithoutCompetitorsQuery();

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var success = (Success<List<CompetitionWithoutCompetitorsDto>>)result;
        success.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenDataProviderThrowsException()
    {
        // Arrange
        Mocker.GetMock<IProvideCanonicalAdminData>()
            .Setup(x => x.GetCompetitionsWithoutCompetitors(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();
        var query = new GetCompetitionsWithoutCompetitorsQuery();

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        result.Should().BeOfType<Failure<List<CompetitionWithoutCompetitorsDto>>>();
        var failure = (Failure<List<CompetitionWithoutCompetitorsDto>>)result;
        failure.Errors.Should().Contain(e => e.ErrorMessage.Contains("Database connection failed"));
    }
}
