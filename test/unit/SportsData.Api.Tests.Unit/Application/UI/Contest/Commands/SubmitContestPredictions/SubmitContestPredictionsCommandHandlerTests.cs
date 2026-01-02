using FluentAssertions;
using SportsData.Api.Application.Common.Enums;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Contest.Commands.SubmitContestPredictions;
using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Contest.Commands.SubmitContestPredictions;

public class SubmitContestPredictionsCommandHandlerTests : ApiTestBase<SubmitContestPredictionsCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenPredictionsIsNull()
    {
        // Arrange
        var sut = Mocker.CreateInstance<SubmitContestPredictionsCommandHandler>();
        var command = new SubmitContestPredictionsCommand
        {
            UserId = Guid.NewGuid(),
            Predictions = null!
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenPredictionsIsEmpty()
    {
        // Arrange
        var sut = Mocker.CreateInstance<SubmitContestPredictionsCommandHandler>();
        var command = new SubmitContestPredictionsCommand
        {
            UserId = Guid.NewGuid(),
            Predictions = new List<ContestPredictionDto>()
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenPredictionsAreValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var predictions = new List<ContestPredictionDto>
        {
            new()
            {
                ContestId = contestId,
                WinnerFranchiseSeasonId = Guid.NewGuid(),
                WinProbability = 0.75m,
                PredictionType = PickType.StraightUp,
                ModelVersion = "v1.0"
            }
        };

        var sut = Mocker.CreateInstance<SubmitContestPredictionsCommandHandler>();
        var command = new SubmitContestPredictionsCommand
        {
            UserId = userId,
            Predictions = predictions
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }
}
