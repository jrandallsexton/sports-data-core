using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Contest.Commands.RefreshContest;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Contest.Commands.RefreshContest;

public class RefreshContestCommandHandlerTests : ApiTestBase<RefreshContestCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnAccepted_WhenRefreshSucceeds()
    {
        // Arrange
        var contestId = Guid.NewGuid();

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.RefreshContestByContestId(contestId))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<RefreshContestCommandHandler>();
        var command = new RefreshContestCommand { ContestId = contestId };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenRefreshThrowsException()
    {
        // Arrange
        var contestId = Guid.NewGuid();

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.RefreshContestByContestId(contestId))
            .ThrowsAsync(new Exception("Refresh failed"));

        var sut = Mocker.CreateInstance<RefreshContestCommandHandler>();
        var command = new RefreshContestCommand { ContestId = contestId };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }
}
