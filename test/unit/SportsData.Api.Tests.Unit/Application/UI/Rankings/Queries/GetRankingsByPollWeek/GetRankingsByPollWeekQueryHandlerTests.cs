using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollWeek;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Rankings.Queries.GetRankingsByPollWeek;

public class GetRankingsByPollWeekQueryHandlerTests : UnitTestBase<GetRankingsByPollWeekQueryHandler>
{
    private readonly Mock<IProvideCanonicalData> _canonicalDataProviderMock;

    public GetRankingsByPollWeekQueryHandlerTests()
    {
        _canonicalDataProviderMock = Mocker.GetMock<IProvideCanonicalData>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenRankingsExist()
    {
        // Arrange
        var rankings = new RankingsByPollIdByWeekDto
        {
            PollId = "ap",
            PollName = "AP Poll",
            SeasonYear = 2025,
            Week = 5,
            Entries =
            [
                new RankingsByPollIdByWeekDto.RankingsByPollIdByWeekEntryDto
                {
                    FranchiseSlug = "alabama",
                    FranchiseName = "Alabama",
                    FranchiseLogoUrl = "http://logo.url",
                    Rank = 1
                }
            ]
        };

        _canonicalDataProviderMock
            .Setup(x => x.GetRankingsByPollIdByWeek("ap", 2025, 5))
            .ReturnsAsync(rankings);

        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2025, Week = 5, Poll = "ap" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PollName.Should().Be("AP Top 25 - Week 5");
        result.Value.HasFirstPlaceVotes.Should().BeTrue();
        result.Value.HasPoints.Should().BeTrue();
        result.Value.HasTrends.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenSeasonYearTooLow()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 1800, Week = 5, Poll = "ap" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        var failure = result as Failure<RankingsByPollIdByWeekDto>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(query.SeasonYear));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenSeasonYearTooHigh()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2200, Week = 5, Poll = "ap" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenWeekTooLow()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2025, Week = 0, Poll = "ap" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        var failure = result as Failure<RankingsByPollIdByWeekDto>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(query.Week));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenWeekTooHigh()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2025, Week = 25, Poll = "ap" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenNoRankingsExist()
    {
        // Arrange
        _canonicalDataProviderMock
            .Setup(x => x.GetRankingsByPollIdByWeek("ap", 2025, 5))
            .ReturnsAsync((RankingsByPollIdByWeekDto)null!);

        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2025, Week = 5, Poll = "ap" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetCoachesPollMetadata_WhenPollIsUsa()
    {
        // Arrange
        var rankings = new RankingsByPollIdByWeekDto
        {
            PollId = "usa",
            PollName = "Coaches",
            SeasonYear = 2025,
            Week = 5,
            Entries = []
        };

        _canonicalDataProviderMock
            .Setup(x => x.GetRankingsByPollIdByWeek("usa", 2025, 5))
            .ReturnsAsync(rankings);

        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2025, Week = 5, Poll = "usa" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PollName.Should().Be("Coaches Poll - Week 5");
        result.Value.HasFirstPlaceVotes.Should().BeTrue();
        result.Value.HasPoints.Should().BeTrue();
        result.Value.HasTrends.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetCfpPollMetadata_WhenPollIsCfp()
    {
        // Arrange
        var rankings = new RankingsByPollIdByWeekDto
        {
            PollId = "cfp",
            PollName = "CFP",
            SeasonYear = 2025,
            Week = 10,
            Entries = []
        };

        _canonicalDataProviderMock
            .Setup(x => x.GetRankingsByPollIdByWeek("cfp", 2025, 10))
            .ReturnsAsync(rankings);

        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2025, Week = 10, Poll = "cfp" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PollName.Should().Be("College Football Playoffs - Week 10");
        result.Value.HasFirstPlaceVotes.Should().BeFalse();
        result.Value.HasPoints.Should().BeFalse();
        result.Value.HasTrends.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDefaultToApPoll_WhenPollIsEmpty()
    {
        // Arrange
        var rankings = new RankingsByPollIdByWeekDto
        {
            PollId = "ap",
            PollName = "AP",
            SeasonYear = 2025,
            Week = 5,
            Entries = []
        };

        _canonicalDataProviderMock
            .Setup(x => x.GetRankingsByPollIdByWeek("ap", 2025, 5))
            .ReturnsAsync(rankings);

        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2025, Week = 5, Poll = "" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PollName.Should().Be("AP Top 25 - Week 5");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNormalizePollToLowerCase()
    {
        // Arrange
        var rankings = new RankingsByPollIdByWeekDto
        {
            PollId = "ap",
            PollName = "AP",
            SeasonYear = 2025,
            Week = 5,
            Entries = []
        };

        _canonicalDataProviderMock
            .Setup(x => x.GetRankingsByPollIdByWeek("ap", 2025, 5))
            .ReturnsAsync(rankings);

        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2025, Week = 5, Poll = "AP" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _canonicalDataProviderMock.Verify(x => x.GetRankingsByPollIdByWeek("ap", 2025, 5), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenExceptionThrown()
    {
        // Arrange
        _canonicalDataProviderMock
            .Setup(x => x.GetRankingsByPollIdByWeek("ap", 2025, 5))
            .ThrowsAsync(new Exception("Database error"));

        var handler = Mocker.CreateInstance<GetRankingsByPollWeekQueryHandler>();
        var query = new GetRankingsByPollWeekQuery { SeasonYear = 2025, Week = 5, Poll = "ap" };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
    }
}
