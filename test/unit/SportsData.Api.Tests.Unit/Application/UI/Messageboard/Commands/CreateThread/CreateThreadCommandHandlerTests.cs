using FluentAssertions;

using SportsData.Api.Application.UI.Messageboard.Commands.CreateThread;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Messageboard.Commands.CreateThread;

public class CreateThreadCommandHandlerTests : ApiTestBase<CreateThreadCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenContentIsEmpty()
    {
        // Arrange
        var sut = Mocker.CreateInstance<CreateThreadCommandHandler>();
        var command = new CreateThreadCommand
        {
            GroupId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Title = "Test Title",
            Content = ""
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenContentIsWhitespace()
    {
        // Arrange
        var sut = Mocker.CreateInstance<CreateThreadCommandHandler>();
        var command = new CreateThreadCommand
        {
            GroupId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Title = "Test Title",
            Content = "   "
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateThread_WhenContentIsValid()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var sut = Mocker.CreateInstance<CreateThreadCommandHandler>();
        var command = new CreateThreadCommand
        {
            GroupId = groupId,
            UserId = userId,
            Title = "Test Title",
            Content = "Test content for the thread"
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.GroupId.Should().Be(groupId);
        result.Value.CreatedBy.Should().Be(userId);
        result.Value.Title.Should().Be("Test Title");
        result.Value.PostCount.Should().Be(1);
    }
}
