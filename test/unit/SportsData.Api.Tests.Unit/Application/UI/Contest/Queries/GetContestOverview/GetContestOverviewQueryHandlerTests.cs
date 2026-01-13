//using FluentAssertions;

//using Moq;

//using SportsData.Api.Application.UI.Contest.Queries.GetContestOverview;
//using SportsData.Api.Infrastructure.Data.Canonical;
//using SportsData.Core.Common;
//using SportsData.Core.Dtos.Canonical;

//using Xunit;

//namespace SportsData.Api.Tests.Unit.Application.UI.Contest.Queries.GetContestOverview;

//public class GetContestOverviewQueryHandlerTests : ApiTestBase<GetContestOverviewQueryHandler>
//{
//    [Fact]
//    public async Task ExecuteAsync_ShouldReturnSuccess_WhenContestExists()
//    {
//        // Arrange
//        var contestId = Guid.NewGuid();
//        var expectedOverview = new ContestOverviewDto();

//        Mocker.GetMock<IProvideCanonicalData>()
//            .Setup(x => x.GetContestOverviewByContestId(contestId))
//            .ReturnsAsync(expectedOverview);

//        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();
//        var query = new GetContestOverviewQuery { ContestId = contestId };

//        // Act
//        var result = await sut.ExecuteAsync(query);

//        // Assert
//        result.IsSuccess.Should().BeTrue();
//        result.Value.Should().NotBeNull();
//    }

//    [Fact]
//    public async Task ExecuteAsync_ShouldReturnNotFound_WhenContestDoesNotExist()
//    {
//        // Arrange
//        var contestId = Guid.NewGuid();

//        Mocker.GetMock<IProvideCanonicalData>()
//            .Setup(x => x.GetContestOverviewByContestId(contestId))
//            .ReturnsAsync((ContestOverviewDto?)null);

//        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();
//        var query = new GetContestOverviewQuery { ContestId = contestId };

//        // Act
//        var result = await sut.ExecuteAsync(query);

//        // Assert
//        result.IsSuccess.Should().BeFalse();
//        result.Status.Should().Be(ResultStatus.NotFound);
//    }
//}
