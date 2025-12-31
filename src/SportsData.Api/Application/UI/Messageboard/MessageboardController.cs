using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Messageboard.Commands.CreateReply;
using SportsData.Api.Application.UI.Messageboard.Commands.CreateThread;
using SportsData.Api.Application.UI.Messageboard.Commands.ToggleReaction;
using SportsData.Api.Application.UI.Messageboard.Dtos;
using SportsData.Api.Application.UI.Messageboard.Queries.GetReplies;
using SportsData.Api.Application.UI.Messageboard.Queries.GetThreads;
using SportsData.Api.Application.UI.Messageboard.Queries.GetThreadsByUserGroups;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

using System.ComponentModel.DataAnnotations;

namespace SportsData.Api.Application.UI.Messageboard;

[ApiController]
[Route("api/messageboard")]
public class MessageboardController : ApiControllerBase
{
    // --- Request models ---

    public sealed record PageQuery(
        [Range(1, 100)] int Limit = 20,
        string? Cursor = null);

    public sealed record CreateThreadRequest(string? Title, [Required] string Content);

    public sealed record CreatePostRequest(Guid? ParentId, [Required] string Content);

    public sealed record ToggleReactionRequest(ReactionType? Type);

    // GET /api/messageboard/my/threads-by-group?perGroupLimit=5
    [HttpGet("my/threads-by-group")]
    public async Task<ActionResult<IDictionary<Guid, PageResult<MessageThread>>>> GetMyThreadsByGroup(
        [FromQuery] int perGroupLimit,
        [FromServices] IGetThreadsByUserGroupsQueryHandler handler,
        CancellationToken ct = default)
    {
        var userId = HttpContext.GetCurrentUserId();
        var query = new GetThreadsByUserGroupsQuery
        {
            UserId = userId,
            PerGroupLimit = perGroupLimit > 0 ? perGroupLimit : 5
        };
        var result = await handler.ExecuteAsync(query, ct);
        return Ok(result);
    }

    // --- Threads ---

    // GET /api/messageboard/groups/{groupId}/threads?limit=20&cursor=...
    [HttpGet("groups/{groupId:guid}/threads")]
    [Authorize]
    public async Task<ActionResult<PageResult<MessageThread>>> GetThreads(
        [FromRoute] Guid groupId,
        [FromQuery] PageQuery q,
        [FromServices] IGetThreadsQueryHandler handler,
        CancellationToken ct)
    {
        var query = new GetThreadsQuery
        {
            GroupId = groupId,
            Limit = q.Limit,
            Cursor = q.Cursor
        };
        var result = await handler.ExecuteAsync(query, ct);
        return Ok(result);
    }

    // POST /api/messageboard/groups/{groupId}/threads
    [HttpPost("groups/{groupId:guid}/threads")]
    [Authorize]
    public async Task<ActionResult<MessageThread>> CreateThread(
        [FromRoute] Guid groupId,
        [FromBody] CreateThreadRequest body,
        [FromServices] ICreateThreadCommandHandler handler,
        CancellationToken ct)
    {
        var userId = HttpContext.GetCurrentUserId();
        var command = new CreateThreadCommand
        {
            GroupId = groupId,
            UserId = userId,
            Title = body.Title,
            Content = body.Content
        };
        var result = await handler.ExecuteAsync(command, ct);
        return result.ToActionResult();
    }

    // --- Posts / Replies ---

    // GET /api/messageboard/threads/{threadId}/posts?parentId={guid/null}&limit=20&cursor=...
    [HttpGet("threads/{threadId:guid}/posts")]
    [Authorize]
    public async Task<ActionResult<PageResult<MessagePost>>> GetReplies(
        [FromRoute] Guid threadId,
        [FromQuery] Guid? parentId,
        [FromQuery] PageQuery q,
        [FromServices] IGetRepliesQueryHandler handler,
        CancellationToken ct)
    {
        var query = new GetRepliesQuery
        {
            ThreadId = threadId,
            ParentId = parentId,
            Limit = q.Limit,
            Cursor = q.Cursor
        };
        var result = await handler.ExecuteAsync(query, ct);
        return Ok(result);
    }

    // POST /api/messageboard/threads/{threadId}/posts
    [HttpPost("threads/{threadId:guid}/posts")]
    [Authorize]
    public async Task<ActionResult<MessagePost>> CreateReply(
        [FromRoute] Guid threadId,
        [FromBody] CreatePostRequest body,
        [FromServices] ICreateReplyCommandHandler handler,
        CancellationToken ct)
    {
        var userId = HttpContext.GetCurrentUserId();
        var command = new CreateReplyCommand
        {
            ThreadId = threadId,
            ParentPostId = body.ParentId,
            UserId = userId,
            Content = body.Content
        };
        var result = await handler.ExecuteAsync(command, ct);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var created = result.Value;
        return Ok(new
        {
            created.Id,
            created.ThreadId,
            created.ParentId,
            created.Depth,
            created.Path,
            created.Content,
            created.CreatedUtc,
            created.CreatedBy,
            created.ReplyCount,
            created.LikeCount,
            created.DislikeCount,
            created.EditedAt,
            created.IsDeleted
        });
    }

    // --- Reactions ---

    // PUT /api/messageboard/posts/{postId}/reaction
    [HttpPut("posts/{postId:guid}/reaction")]
    [Authorize]
    public async Task<ActionResult<ReactionType?>> PutReaction(
        [FromRoute] Guid postId,
        [FromBody] ToggleReactionRequest body,
        [FromServices] IToggleReactionCommandHandler handler,
        CancellationToken ct)
    {
        var userId = HttpContext.GetCurrentUserId();
        var command = new ToggleReactionCommand
        {
            PostId = postId,
            UserId = userId,
            Type = body.Type
        };
        var result = await handler.ExecuteAsync(command, ct);
        return result.ToActionResult();
    }

    // DELETE /api/messageboard/posts/{postId}/reaction
    [HttpDelete("posts/{postId:guid}/reaction")]
    [Authorize]
    public async Task<IActionResult> DeleteReaction(
        [FromRoute] Guid postId,
        [FromServices] IToggleReactionCommandHandler handler,
        CancellationToken ct)
    {
        var userId = HttpContext.GetCurrentUserId();
        var command = new ToggleReactionCommand
        {
            PostId = postId,
            UserId = userId,
            Type = null
        };
        await handler.ExecuteAsync(command, ct);
        return NoContent();
    }
}
