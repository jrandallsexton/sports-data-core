using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Messageboard;

public interface IMessageboardService
{
    Task<IDictionary<Guid, PageResult<MessageThread>>> GetThreadsByUserGroupsAsync(
        Guid userId, int perGroupLimit, CancellationToken ct = default);

    Task<PageResult<MessageThread>> GetThreadsAsync(
        Guid groupId, PageRequest page, CancellationToken ct = default);

    Task<MessageThread> CreateThreadAsync(
        Guid groupId, Guid userId, string? title, string content, CancellationToken ct = default);

    Task<PageResult<MessagePost>> GetRepliesAsync(
        Guid threadId, Guid? parentId, PageRequest page, CancellationToken ct = default);

    Task<MessagePost> CreateReplyAsync(
        Guid threadId, Guid? parentPostId, Guid userId, string content, CancellationToken ct = default);

    Task<ReactionType?> ToggleReactionAsync(
        Guid postId, Guid userId, ReactionType? type, CancellationToken ct = default);
}