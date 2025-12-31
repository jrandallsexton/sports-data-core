using FluentAssertions;

using SportsData.Api.Application.UI.Messageboard.Commands.CreateReply;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Messageboard.Commands.CreateReply;

public class CreateReplyCommandHandlerTests : ApiTestBase<CreateReplyCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenContentIsEmpty()
    {
        // Arrange
        var sut = Mocker.CreateInstance<CreateReplyCommandHandler>();
        var command = new CreateReplyCommand
        {
            ThreadId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Content = ""
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenThreadDoesNotExist()
    {
        // Arrange
        var sut = Mocker.CreateInstance<CreateReplyCommandHandler>();
        var command = new CreateReplyCommand
        {
            ThreadId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Content = "Test reply content"
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateReply_WhenThreadExists()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var thread = new MessageThread
        {
            Id = threadId,
            GroupId = Guid.NewGuid(),
            CreatedBy = userId,
            CreatedUtc = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            PostCount = 1
        };
        await DataContext.Set<MessageThread>().AddAsync(thread);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<CreateReplyCommandHandler>();
        var command = new CreateReplyCommand
        {
            ThreadId = threadId,
            UserId = userId,
            Content = "Test reply content"
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ThreadId.Should().Be(threadId);
        result.Value.Content.Should().Be("Test reply content");
    }
}
