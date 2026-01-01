using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsBySeasonYear;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Rankings.Queries.GetRankingsBySeasonYear;

public class GetRankingsBySeasonYearQueryHandlerTests : UnitTestBase<GetRankingsBySeasonYearQueryHandler>
{
    private readonly Mock<IProvideCanonicalData> _canonicalDataProviderMock;

    public GetRankingsBySeasonYearQueryHandlerTests()
    {
        _canonicalDataProviderMock = Mocker.GetMock<IProvideCanonicalData>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenPollsExist()
    {
        // Arrange
        var polls = new List<FranchiseSeasonPollDto>
        {
            new()
            {
                PollId = "ap",
                PollName = "AP Top 25",
                SeasonYear = 2025,
                Week = 1,
                Entries = []
            },
            new()
            {
                PollId = "usa",
                PollName = "Coaches Poll",
                SeasonYear = 2025,
                Week = 1,
                Entries = []
            }
        };

        _canonicalDataProviderMock
            .Setup(x => x.GetFranchiseSeasonRankings(2025))
            .ReturnsAsync(polls);

        var handler = Mocker.CreateInstance<GetRankingsBySeasonYearQueryHandler>();
        var query = new GetRankingsBySeasonYearQuery { SeasonYear = 2025 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenNoPollsExist()
    {
        // Arrange
        _canonicalDataProviderMock
            .Setup(x => x.GetFranchiseSeasonRankings(2025))
            .ReturnsAsync(new List<FranchiseSeasonPollDto>());

        var handler = Mocker.CreateInstance<GetRankingsBySeasonYearQueryHandler>();
        var query = new GetRankingsBySeasonYearQuery { SeasonYear = 2025 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenPollsAreNull()
    {
        // Arrange
        _canonicalDataProviderMock
            .Setup(x => x.GetFranchiseSeasonRankings(2025))
            .ReturnsAsync((List<FranchiseSeasonPollDto>?)null);

        var handler = Mocker.CreateInstance<GetRankingsBySeasonYearQueryHandler>();
        var query = new GetRankingsBySeasonYearQuery { SeasonYear = 2025 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenExceptionThrown()
    {
        // Arrange
        _canonicalDataProviderMock
            .Setup(x => x.GetFranchiseSeasonRankings(2025))
            .ThrowsAsync(new Exception("Database error"));

        var handler = Mocker.CreateInstance<GetRankingsBySeasonYearQueryHandler>();
        var query = new GetRankingsBySeasonYearQuery { SeasonYear = 2025 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
    }
}
