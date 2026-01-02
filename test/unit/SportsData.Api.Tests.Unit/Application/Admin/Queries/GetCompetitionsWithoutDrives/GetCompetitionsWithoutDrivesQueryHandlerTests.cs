using FluentAssertions;

using Moq;

using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutDrives;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Queries.GetCompetitionsWithoutDrives;

public class GetCompetitionsWithoutDrivesQueryHandlerTests : ApiTestBase<GetCompetitionsWithoutDrivesQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenDataProviderReturnsResults()
    {
        // Arrange
        var expectedResults = new List<CompetitionWithoutDrivesDto>
        {
            new() { CompetitionId = Guid.NewGuid(), ContestName = "Test Competition 1" },
            new() { CompetitionId = Guid.NewGuid(), ContestName = "Test Competition 2" }
        };

        Mocker.GetMock<IProvideCanonicalAdminData>()
            .Setup(x => x.GetCompetitionsWithoutDrives())
            .ReturnsAsync(expectedResults);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutDrivesQueryHandler>();
        var query = new GetCompetitionsWithoutDrivesQuery();

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<Success<List<CompetitionWithoutDrivesDto>>>();
        var success = (Success<List<CompetitionWithoutDrivesDto>>)result;
        success.Value.Should().HaveCount(2);
        success.Value.Should().BeEquivalentTo(expectedResults);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenNoResultsFound()
    {
        // Arrange
        var expectedResults = new List<CompetitionWithoutDrivesDto>();

        Mocker.GetMock<IProvideCanonicalAdminData>()
            .Setup(x => x.GetCompetitionsWithoutDrives())
            .ReturnsAsync(expectedResults);

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutDrivesQueryHandler>();
        var query = new GetCompetitionsWithoutDrivesQuery();

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var success = (Success<List<CompetitionWithoutDrivesDto>>)result;
        success.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenDataProviderThrowsException()
    {
        // Arrange
        Mocker.GetMock<IProvideCanonicalAdminData>()
            .Setup(x => x.GetCompetitionsWithoutDrives())
            .ThrowsAsync(new Exception("Database error"));

        var handler = Mocker.CreateInstance<GetCompetitionsWithoutDrivesQueryHandler>();
        var query = new GetCompetitionsWithoutDrivesQuery();

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        result.Should().BeOfType<Failure<List<CompetitionWithoutDrivesDto>>>();
        var failure = (Failure<List<CompetitionWithoutDrivesDto>>)result;
        failure.Errors.Should().Contain(e => e.ErrorMessage.Contains("Database error"));
    }
}
