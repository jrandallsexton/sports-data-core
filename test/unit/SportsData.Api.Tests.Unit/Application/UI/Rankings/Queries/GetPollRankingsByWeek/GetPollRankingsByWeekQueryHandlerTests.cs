using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Application.UI.Rankings.Queries.GetPollRankingsByWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollWeek;
using SportsData.Core.Common;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Rankings.Queries.GetPollRankingsByWeek;

public class GetPollRankingsByWeekQueryHandlerTests : UnitTestBase<GetPollRankingsByWeekQueryHandler>
{
    private readonly Mock<IGetRankingsByPollWeekQueryHandler> _rankingsByPollWeekHandlerMock;

    public GetPollRankingsByWeekQueryHandlerTests()
    {
        _rankingsByPollWeekHandlerMock = Mocker.GetMock<IGetRankingsByPollWeekQueryHandler>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAllPolls_WhenAllSucceed()
    {
        // Arrange
        var cfpRankings = new RankingsByPollIdByWeekDto
        {
            PollId = "cfp",
            PollName = "College Football Playoffs - Week 10",
            SeasonYear = 2025,
            Week = 10,
            Entries = []
        };

        var apRankings = new RankingsByPollIdByWeekDto
        {
            PollId = "ap",
            PollName = "AP Top 25 - Week 10",
            SeasonYear = 2025,
            Week = 10,
            Entries = []
        };

        var coachesRankings = new RankingsByPollIdByWeekDto
        {
            PollId = "usa",
            PollName = "Coaches Poll - Week 10",
            SeasonYear = 2025,
            Week = 10,
            Entries = []
        };

        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetRankingsByPollWeekQuery>(q => q.Poll == "cfp"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<RankingsByPollIdByWeekDto>(cfpRankings));

        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetRankingsByPollWeekQuery>(q => q.Poll == "ap"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<RankingsByPollIdByWeekDto>(apRankings));

        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetRankingsByPollWeekQuery>(q => q.Poll == "usa"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<RankingsByPollIdByWeekDto>(coachesRankings));

        var handler = Mocker.CreateInstance<GetPollRankingsByWeekQueryHandler>();
        var query = new GetPollRankingsByWeekQuery { SeasonYear = 2025, Week = 10 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().Contain(r => r.PollId == "cfp");
        result.Value.Should().Contain(r => r.PollId == "ap");
        result.Value.Should().Contain(r => r.PollId == "usa");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnPollsInOrder_CfpApCoaches()
    {
        // Arrange
        var cfpRankings = new RankingsByPollIdByWeekDto
        {
            PollId = "cfp",
            PollName = "CFP",
            SeasonYear = 2025,
            Week = 10,
            Entries = []
        };

        var apRankings = new RankingsByPollIdByWeekDto
        {
            PollId = "ap",
            PollName = "AP",
            SeasonYear = 2025,
            Week = 10,
            Entries = []
        };

        var coachesRankings = new RankingsByPollIdByWeekDto
        {
            PollId = "usa",
            PollName = "Coaches",
            SeasonYear = 2025,
            Week = 10,
            Entries = []
        };

        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetRankingsByPollWeekQuery>(q => q.Poll == "cfp"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<RankingsByPollIdByWeekDto>(cfpRankings));

        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetRankingsByPollWeekQuery>(q => q.Poll == "ap"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<RankingsByPollIdByWeekDto>(apRankings));

        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetRankingsByPollWeekQuery>(q => q.Poll == "usa"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<RankingsByPollIdByWeekDto>(coachesRankings));

        var handler = Mocker.CreateInstance<GetPollRankingsByWeekQueryHandler>();
        var query = new GetPollRankingsByWeekQuery { SeasonYear = 2025, Week = 10 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value[0].PollId.Should().Be("cfp");
        result.Value[1].PollId.Should().Be("ap");
        result.Value[2].PollId.Should().Be("usa");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlySuccessfulPolls_WhenSomeFail()
    {
        // Arrange
        var apRankings = new RankingsByPollIdByWeekDto
        {
            PollId = "ap",
            PollName = "AP Top 25",
            SeasonYear = 2025,
            Week = 5,
            Entries = []
        };

        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetRankingsByPollWeekQuery>(q => q.Poll == "cfp"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<RankingsByPollIdByWeekDto>(default!, ResultStatus.NotFound, []));

        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetRankingsByPollWeekQuery>(q => q.Poll == "ap"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<RankingsByPollIdByWeekDto>(apRankings));

        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.Is<GetRankingsByPollWeekQuery>(q => q.Poll == "usa"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<RankingsByPollIdByWeekDto>(default!, ResultStatus.NotFound, []));

        var handler = Mocker.CreateInstance<GetPollRankingsByWeekQueryHandler>();
        var query = new GetPollRankingsByWeekQuery { SeasonYear = 2025, Week = 5 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].PollId.Should().Be("ap");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenAllPollsFail()
    {
        // Arrange
        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<GetRankingsByPollWeekQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<RankingsByPollIdByWeekDto>(default!, ResultStatus.NotFound, []));

        var handler = Mocker.CreateInstance<GetPollRankingsByWeekQueryHandler>();
        var query = new GetPollRankingsByWeekQuery { SeasonYear = 2025, Week = 5 };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCorrectSeasonYearAndWeek_ToUnderlyingHandler()
    {
        // Arrange
        _rankingsByPollWeekHandlerMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<GetRankingsByPollWeekQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<RankingsByPollIdByWeekDto>(default!, ResultStatus.NotFound, []));

        var handler = Mocker.CreateInstance<GetPollRankingsByWeekQueryHandler>();
        var query = new GetPollRankingsByWeekQuery { SeasonYear = 2024, Week = 8 };

        // Act
        await handler.ExecuteAsync(query);

        // Assert
        _rankingsByPollWeekHandlerMock.Verify(x => x.ExecuteAsync(
            It.Is<GetRankingsByPollWeekQuery>(q => q.SeasonYear == 2024 && q.Week == 8 && q.Poll == "cfp"),
            It.IsAny<CancellationToken>()), Times.Once);

        _rankingsByPollWeekHandlerMock.Verify(x => x.ExecuteAsync(
            It.Is<GetRankingsByPollWeekQuery>(q => q.SeasonYear == 2024 && q.Week == 8 && q.Poll == "ap"),
            It.IsAny<CancellationToken>()), Times.Once);

        _rankingsByPollWeekHandlerMock.Verify(x => x.ExecuteAsync(
            It.Is<GetRankingsByPollWeekQuery>(q => q.SeasonYear == 2024 && q.Week == 8 && q.Poll == "usa"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
