using FluentAssertions;
using FluentValidation.Results;

using Moq;

using SportsData.Api.Application.UI.Contest.Commands.FinalizeContest;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Contest.Commands.FinalizeContest;

public class FinalizeContestCommandHandlerTests : ApiTestBase<FinalizeContestCommandHandler>
{
    private readonly Mock<IProvideContests> _contestClientMock;
    private readonly Mock<IContestClientFactory> _contestClientFactoryMock;

    public FinalizeContestCommandHandlerTests()
    {
        _contestClientMock = new Mock<IProvideContests>();
        _contestClientFactoryMock = Mocker.GetMock<IContestClientFactory>();
        _contestClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAccepted_WhenFinalizeSucceeds()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var sport = Sport.BaseballMlb;

        _contestClientMock
            .Setup(x => x.FinalizeContestByContestId(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<bool>(true, ResultStatus.Accepted));

        var sut = Mocker.CreateInstance<FinalizeContestCommandHandler>();
        var command = new FinalizeContestCommand { ContestId = contestId, Sport = sport };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        _contestClientFactoryMock.Verify(x => x.Resolve(sport), Times.Once);
        _contestClientMock.Verify(
            x => x.FinalizeContestByContestId(contestId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenFinalizeFails()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var sport = Sport.BaseballMlb;
        var validationFailures = new List<ValidationFailure>
        {
            new("contestId", "downstream finalize failed")
        };

        _contestClientMock
            .Setup(x => x.FinalizeContestByContestId(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<bool>(false, ResultStatus.BadRequest, validationFailures));

        var sut = Mocker.CreateInstance<FinalizeContestCommandHandler>();
        var command = new FinalizeContestCommand { ContestId = contestId, Sport = sport };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }
}
