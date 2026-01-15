using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsBySeasonYear;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Rankings.Queries.GetRankingsBySeasonYear;

public class GetRankingsBySeasonYearQueryHandlerTests : UnitTestBase<GetRankingsBySeasonYearQueryHandler>
{
    private readonly Mock<IProvideFranchises> _franchiseClientMock;
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;

    public GetRankingsBySeasonYearQueryHandlerTests()
    {
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenPollsExist()
    {
        // Arrange
        var sport = Sport.FootballNcaa;
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

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonRankings(2025, It.IsAny<CancellationToken>()))
            .ReturnsAsync(polls);

        var handler = Mocker.CreateInstance<GetRankingsBySeasonYearQueryHandler>();
        var query = new GetRankingsBySeasonYearQuery { SeasonYear = 2025, Sport = sport };

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
        var sport = Sport.FootballNcaa;
        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonRankings(2025, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FranchiseSeasonPollDto>());

        var handler = Mocker.CreateInstance<GetRankingsBySeasonYearQueryHandler>();
        var query = new GetRankingsBySeasonYearQuery { SeasonYear = 2025, Sport = sport };

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
        var sport = Sport.FootballNcaa;
        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonRankings(2025, It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<FranchiseSeasonPollDto>)null!);

        var handler = Mocker.CreateInstance<GetRankingsBySeasonYearQueryHandler>();
        var query = new GetRankingsBySeasonYearQuery { SeasonYear = 2025, Sport = sport };

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
        var sport = Sport.FootballNcaa;
        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonRankings(2025, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var handler = Mocker.CreateInstance<GetRankingsBySeasonYearQueryHandler>();
        var query = new GetRankingsBySeasonYearQuery { SeasonYear = 2025, Sport = sport };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
    }
}
