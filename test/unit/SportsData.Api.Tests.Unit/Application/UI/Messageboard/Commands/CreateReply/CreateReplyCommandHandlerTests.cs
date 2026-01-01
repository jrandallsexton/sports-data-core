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

    [Fact]
    public async Task ExecuteAsync_ShouldAssignUniquePaths_ToMultipleRootLevelReplies()
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
            PostCount = 0
        };
        await DataContext.Set<MessageThread>().AddAsync(thread);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<CreateReplyCommandHandler>();

        // Act - Create three root-level replies (no ParentPostId)
        var result1 = await sut.ExecuteAsync(new CreateReplyCommand
        {
            ThreadId = threadId,
            UserId = userId,
            Content = "First reply"
        });

        var result2 = await sut.ExecuteAsync(new CreateReplyCommand
        {
            ThreadId = threadId,
            UserId = userId,
            Content = "Second reply"
        });

        var result3 = await sut.ExecuteAsync(new CreateReplyCommand
        {
            ThreadId = threadId,
            UserId = userId,
            Content = "Third reply"
        });

        // Assert - Each should have a unique path
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result3.IsSuccess.Should().BeTrue();

        result1.Value.Path.Should().Be("0001");
        result2.Value.Path.Should().Be("0002");
        result3.Value.Path.Should().Be("0003");

        // Verify paths are unique
        var paths = new[] { result1.Value.Path, result2.Value.Path, result3.Value.Path };
        paths.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAssignNestedPaths_ToChildReplies()
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
            PostCount = 0
        };
        await DataContext.Set<MessageThread>().AddAsync(thread);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<CreateReplyCommandHandler>();

        // Act - Create a root reply, then nested replies
        var rootResult = await sut.ExecuteAsync(new CreateReplyCommand
        {
            ThreadId = threadId,
            UserId = userId,
            Content = "Root reply"
        });

        var childResult1 = await sut.ExecuteAsync(new CreateReplyCommand
        {
            ThreadId = threadId,
            ParentPostId = rootResult.Value.Id,
            UserId = userId,
            Content = "First child"
        });

        var childResult2 = await sut.ExecuteAsync(new CreateReplyCommand
        {
            ThreadId = threadId,
            ParentPostId = rootResult.Value.Id,
            UserId = userId,
            Content = "Second child"
        });

        // Assert
        rootResult.Value.Path.Should().Be("0001");
        rootResult.Value.Depth.Should().Be(1);

        childResult1.Value.Path.Should().Be("0001.0001");
        childResult1.Value.Depth.Should().Be(2);

        childResult2.Value.Path.Should().Be("0001.0002");
        childResult2.Value.Depth.Should().Be(2);
    }
}
