using FluentAssertions;

using SportsData.Api.Application.UI.Messageboard.Commands.ToggleReaction;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Messageboard.Commands.ToggleReaction;

public class ToggleReactionCommandHandlerTests : ApiTestBase<ToggleReactionCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenPostDoesNotExist()
    {
        // Arrange
        var sut = Mocker.CreateInstance<ToggleReactionCommandHandler>();
        var command = new ToggleReactionCommand
        {
            PostId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Type = ReactionType.Like
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAddReaction_WhenPostExistsAndNoExistingReaction()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var post = new MessagePost
        {
            Id = postId,
            ThreadId = Guid.NewGuid(),
            Content = "Test post",
            CreatedBy = userId,
            CreatedUtc = DateTime.UtcNow,
            Depth = 0,
            Path = "0001",
            LikeCount = 0,
            DislikeCount = 0
        };
        await DataContext.Set<MessagePost>().AddAsync(post);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<ToggleReactionCommandHandler>();
        var command = new ToggleReactionCommand
        {
            PostId = postId,
            UserId = userId,
            Type = ReactionType.Like
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ReactionType.Like);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClearReaction_WhenTypeIsNull()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var post = new MessagePost
        {
            Id = postId,
            ThreadId = Guid.NewGuid(),
            Content = "Test post",
            CreatedBy = userId,
            CreatedUtc = DateTime.UtcNow,
            Depth = 0,
            Path = "0001",
            LikeCount = 1,
            DislikeCount = 0
        };
        await DataContext.Set<MessagePost>().AddAsync(post);

        var reaction = new MessageReaction
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            UserId = userId,
            Type = ReactionType.Like,
            CreatedBy = userId,
            CreatedUtc = DateTime.UtcNow
        };
        await DataContext.Set<MessageReaction>().AddAsync(reaction);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<ToggleReactionCommandHandler>();
        var command = new ToggleReactionCommand
        {
            PostId = postId,
            UserId = userId,
            Type = null
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
