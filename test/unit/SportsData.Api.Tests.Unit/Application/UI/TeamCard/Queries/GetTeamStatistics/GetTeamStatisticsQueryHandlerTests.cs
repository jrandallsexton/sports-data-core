using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.TeamCard;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamStatistics;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.TeamCard.Queries.GetTeamStatistics;

public class GetTeamStatisticsQueryHandlerTests : UnitTestBase<GetTeamStatisticsQueryHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;
    private readonly Mock<IStatFormattingService> _statFormattingServiceMock;

    public GetTeamStatisticsQueryHandlerTests()
    {
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);
        _statFormattingServiceMock = Mocker.GetMock<IStatFormattingService>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenStatisticsExist()
    {
        // Arrange
        var franchiseSeasonId = Guid.NewGuid();
        var statistics = new FranchiseSeasonStatisticDto
        {
            GamesPlayed = 12,
            Statistics = new Dictionary<string, List<FranchiseSeasonStatisticDto.FranchiseSeasonStatisticEntry>>()
        };

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonStatistics(franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(statistics);

        var handler = Mocker.CreateInstance<GetTeamStatisticsQueryHandler>();
        var query = new GetTeamStatisticsQuery { FranchiseSeasonId = franchiseSeasonId };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.GamesPlayed.Should().Be(12);
        _statFormattingServiceMock.Verify(
            x => x.ApplyFriendlyLabelsAndFormatting(statistics, It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenFranchiseSeasonIdIsEmpty()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetTeamStatisticsQueryHandler>();
        var query = new GetTeamStatisticsQuery { FranchiseSeasonId = Guid.Empty };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        var failure = result as Failure<FranchiseSeasonStatisticDto>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(query.FranchiseSeasonId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenStatisticsDoNotExist()
    {
        // Arrange
        var franchiseSeasonId = Guid.NewGuid();

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonStatistics(franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FranchiseSeasonStatisticDto)null!);

        var handler = Mocker.CreateInstance<GetTeamStatisticsQueryHandler>();
        var query = new GetTeamStatisticsQuery { FranchiseSeasonId = franchiseSeasonId };

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
        var franchiseSeasonId = Guid.NewGuid();

        _franchiseClientMock
            .Setup(x => x.GetFranchiseSeasonStatistics(franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("HTTP error"));

        var handler = Mocker.CreateInstance<GetTeamStatisticsQueryHandler>();
        var query = new GetTeamStatisticsQuery { FranchiseSeasonId = franchiseSeasonId };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
    }
}
